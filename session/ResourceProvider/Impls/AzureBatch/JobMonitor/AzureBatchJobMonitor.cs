// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ResourceProvider.Impls.AzureBatch
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlTypes;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Common;
    using Newtonsoft.Json;
    using System.IO;
    using System.Reflection;
    using Microsoft.Telepathy.Session;
    using Microsoft.Telepathy.ResourceProvider;
    using TaskState = Azure.Batch.Common.TaskState;
    using TelepathyBatchClient = Telepathy.Session.BatchClient;
    using Microsoft.Telepathy.Common;
    using StackExchange.Redis;

    internal class AzureBatchJobMonitor : IDisposable
    {
        /// <summary>
        /// The session id
        /// </summary>
        private readonly string _sessionId;

        /// <summary>
        /// Service job
        /// </summary>
        private CloudJob cloudJob;

        /// <summary>
        /// Stores the dispose flag
        /// </summary>
        private int disposeFlag;

        /// <summary>
        /// Stores the min time gap between two pull task operations
        /// </summary>
        private const int PullJobMinGap = 1000;

        /// <summary>
        /// Stores the max time gap between two pull task operations (if required)
        /// </summary>
        private const int PullJobMaxGap = 10000;

        /// <summary>
        /// Stores the current pull task gap for two continuously request
        /// </summary>
        private int pullJobGap = PullJobMinGap;

        private Azure.Batch.BatchClient batchClient;

        /// <summary>
        /// Gets or sets the exit event
        /// </summary>
        public event EventHandler Exit;

        /// <summary>
        /// Gets or sets the report state event
        /// </summary>
        public Action<SessionState, List<TaskInfo>, bool> ReportJobStateAction;

        /// <summary>
        /// Stores the last change time
        /// </summary>
        private DateTime lastChangeTime;

        private SessionState previousJobState = SessionState.Creating;

        private int SessionTimeout;

        private readonly IDatabase _cache;

        private SKU[] loadSKUs()
        {
            try
            {
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitor] Start to load SKUs file.");
                string content = File.ReadAllText($"{Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location)}/SKUs.json");
                SKU[] skus = JsonConvert.DeserializeObject<SKU[]>(content);
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitor] SKUs file loaded: {0} items", skus.Length);
                return skus;
            }
            catch (Exception e)
            {
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Error, "[AzureBatchJobMonitor] Error occurs when loading skus file: {0}", e);
                throw;
            }
        }

        private SKU[] skus;

        private int nodeCapacity;
 

        /// <summary>
        /// Initializes a new instance of the JobMonitorEntry class
        /// </summary>
        /// <param name="sessionid">indicating the session id</param>
        public AzureBatchJobMonitor(string sessionid, Action<SessionState, List<TaskInfo>, bool> reportJobStateAction, int sessionTimeout = 10000)
        {
            this._sessionId = sessionid;
            this.batchClient = AzureBatchConfiguration.GetBatchClient();
            this.ReportJobStateAction = reportJobStateAction;
            this.lastChangeTime = SqlDateTime.MinValue.Value;
            this.skus = loadSKUs();
            this._cache = RedisDB.Connection.GetDatabase();
            SessionTimeout = sessionTimeout;
        }

        /// <summary>
        /// Start the monitor
        /// </summary>
        public async Task StartAsync()
        {
            //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitor] Start Azure Batch job monitor.");
            this.cloudJob = await this.batchClient.JobOperations.GetJobAsync(AzureBatchSessionJobIdConverter.ConvertToAzureBatchJobId(this._sessionId));

            try
            {
                await this.QueryJobChangeAsync();
            }
            catch (Exception e)
            {
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Warning, "[AzureBatchJobMonitor] Exception thrown when trigger querying job info task: {0}", e);
            }
        }

        /// <summary>
        /// Query job info
        /// </summary>
        private async Task QueryJobChangeAsync()
        {
            //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Verbose,
            //  "[AzureBatchJobMonitorEntry] Enters QueryTaskInfo method.");
            bool shouldExit = false;
            this.pullJobGap = PullJobMinGap;
            JobState state = JobState.Active;
            SessionState currentJobState = SessionState.Creating;

            var pool = await this.batchClient.PoolOperations.GetPoolAsync(AzureBatchConfiguration.AzureBatchPoolName);
            string skuName = pool.VirtualMachineSize;
            SKU targetSku = Array.Find(this.skus, sku => sku.Name.Equals(skuName, StringComparison.OrdinalIgnoreCase));
            //What if targetSku is null?
            this.nodeCapacity = targetSku.VCPUs;

            ODATADetailLevel detailLevel = new ODATADetailLevel();
            detailLevel.SelectClause = "affinityId, ipAddress";
            var nodes = await pool.ListComputeNodes(detailLevel).ToListAsync();
            DateTime sessionIdleTimestamp = DateTime.MinValue;
            while (true)
            {
                if (shouldExit)
                {
                    break;
                }
                List<TaskInfo> stateChangedTaskList = new List<TaskInfo>();

                try
                {
                    //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Verbose, "[AzureBatchJobMonitor] Starting get job state.");
                    ODATADetailLevel detail = new ODATADetailLevel(selectClause: "state");
                    this.cloudJob = await this.batchClient.JobOperations.GetJobAsync(this.cloudJob.Id);
                    state = this.cloudJob.State.HasValue ? this.cloudJob.State.Value : state;
                    currentJobState = await AzureBatchJobStateConverter.FromAzureBatchJobAsync(this.cloudJob);
                    Console.WriteLine($"Current job state from AzureBatchJobStateConverter is {currentJobState}");
                    if (state == JobState.Completed || state == JobState.Disabled)
                    {
                        if (this.previousJobState == SessionState.Canceling)
                        {
                            currentJobState = SessionState.Canceled;
                        }
                        shouldExit = true;
                    }
                    else if (this.previousJobState == SessionState.Canceling && !shouldExit)
                    {
                        //Override current job state as Canceling, because when all tasks turn to be completed, the job state converter will make job state finishing.
                        //If one job is cancelling in previous state and now is not in one terminated state, keep to reporting cancelling state to job monitor entry.
                        currentJobState = SessionState.Canceling;
                        //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitor] Overwrite current job state as {0} in Telepathy according to previous job state {1}\n", currentJobState, previousJobState);
                    }
                    else if (currentJobState == SessionState.Failed)
                    {
                        shouldExit = true;
                    }
                    else if (currentJobState == SessionState.Running && this.previousJobState == SessionState.Completed)
                    {
                        currentJobState = SessionState.Completed;
                    }
                    else if (currentJobState == SessionState.Running || currentJobState == SessionState.Finishing)
                    {
                        //query all clients from Redis
                        var hashKey = SessionConfigurationManager.GetRedisBatchClientStateKey(_sessionId);
                        var allClientsState = _cache.HashValues(hashKey);
                        bool allClientsTimeout = true;
                        bool allClientsFinished = true;
                        foreach (var clientState in allClientsState)
                        {
                            BatchClientState currentBatchClientState = (BatchClientState)Enum.Parse(typeof(BatchClientState), clientState.ToString());
                            if (!currentBatchClientState.Equals(BatchClientState.EndOfResponse))
                            {
                                allClientsFinished = false;
                            }
                            else if (!currentBatchClientState.Equals(BatchClientState.Timeout))
                            {
                                allClientsTimeout = false;
                            }

                            if (!allClientsTimeout && !allClientsFinished)
                            {
                                break;
                            }
                        }

                    /*    if (allClientsTimeout && currentJobState == SessionState.Running)
                        {
                            Console.WriteLine($"[AzureBatchJobMonitor] All batch clients are timeout and session will be set Running.");
                            currentJobState = SessionState.Failed;
                            shouldExit = true;
                        }
                        else if (allClientsFinished && this.previousJobState == SessionState.Idle)
                        {
                            //check if session is timeout to be idle state
                            DateTime currentTime = DateTime.Now;

                            if (sessionIdleTimestamp != DateTime.MinValue && (currentTime - sessionIdleTimestamp) >= TimeSpan.FromMilliseconds(SessionTimeout))
                            {
                                currentJobState = SessionState.Completed;

                                //TODO:
                                //Do resource clean up work later
                            }
                            else
                            {
                                currentJobState = SessionState.Idle;
                            }
                        }
                        else if (allClientsFinished && currentJobState == SessionState.Running)
                        {
                            currentJobState = SessionState.Idle;
                            sessionIdleTimestamp = DateTime.Now;
                        }
                        else if (!allClientsFinished && currentJobState == SessionState.Finishing)
                        {
                            currentJobState = SessionState.Canceling;
                        }
                    */
                    }

                    stateChangedTaskList = await this.GetTaskStateChangeAsync(nodes);
                }
                catch (BatchException e)
                {
                    //If the previous job state is canceling and current job is not found, then the job is deleted.
                    if (e.RequestInformation != null & e.RequestInformation.HttpStatusCode != null)
                    {
                        if (e.RequestInformation.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
                        {
                            if (previousJobState == SessionState.Canceling)
                            {
                                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Warning, "[AzureBatchJobMonitor] The queried job has been deleted.");
                                Console.WriteLine("The queried job has been deleted.");
                            }
                            else
                            {
                                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Warning, "[AzureBatchJobMonitor] The queried job previous state is {0}, we make its state as canceled because it's no longer exist.", previousJobState);
                                Console.WriteLine($"The queried job previous state is {previousJobState}, we make its state canceled because it's no longer exist.");
                            }
                            shouldExit = true;
                            currentJobState = SessionState.Canceled;
                        }
                    }
                }
                catch (Exception e)
                {
                    //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Warning, "[AzureBatchJobMonitor] Exception thrown when querying job info: {0}", e);
                }

                try
                {
                    if (this.ReportJobStateAction != null)
                    {
                        /*TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information,
                            "[AzureBatchJobMonitor] Current job state report to AzureBatchJobMonitorEntry: JobState = {0}\n",
                            currentJobState);*/

                        Console.WriteLine($"[AzureBatchJobMonitor] {_sessionId} : Current job state is {currentJobState}");
                        this.ReportJobStateAction(currentJobState, stateChangedTaskList, shouldExit);
                    }
                }
                catch (Exception e)
                {
                    //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Warning, "[AzureBatchJobMonitor] Exception thrown when report job info: {0}", e);
                }

                this.previousJobState = currentJobState;

                if (!shouldExit)
                {
                    //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitor] Waiting {0} milliseconds and start another round of getting job state info.", this.pullJobGap);

                    //Sleep and pull job again, clear the register pull job flag
                    await Task.Delay(this.pullJobGap);
                    if (this.pullJobGap < PullJobMaxGap)
                    {
                        this.pullJobGap *= 2;
                        if (this.pullJobGap > PullJobMaxGap)
                        {
                            this.pullJobGap = PullJobMaxGap;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets the task info
        /// </summary>
        /// <returns>returns the task info as a dictionary, keyed by task id</returns>
        /// <remarks>
        /// This method returns a list of task info which ChangeTime property is in this rank: [this.lastChangeTime, DateTime.Now].
        /// </remarks>
        private async Task<List<TaskInfo>> GetTaskStateChangeAsync(List<ComputeNode> nodes)
        {
            try
            {
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Verbose, "[AzureBatchJobMonitor] Query task info...");
                ODATADetailLevel detail = new ODATADetailLevel(filterClause: $"(stateTransitionTime ge datetime'{this.lastChangeTime:O}')", selectClause: "id,nodeInfo,state,stateTransitionTime,executionInfo");
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitor] Query task info filter clause = {0}\n", detail.FilterClause);
                List<CloudTask> stateChangedTasks = await this.batchClient.JobOperations.ListTasks(this.cloudJob.Id, detail).ToListAsync();
                if (stateChangedTasks.Count == 0)
                {
                    // no service task dispathed yet.
                    /*TraceHelper.TraceEvent(this.sessionid, TraceEventType.Warning,
                        "[AzureBatchJobMonitorEntry] Failed to get tasks or no task state change.");*/
                    return null;
                }

                List<TaskInfo> results = new List<TaskInfo>(stateChangedTasks.Count);
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitor] The number of changed state tasks is {0}", stateChangedTasks.Count);
                DateTime lastStateTransitionTime = new DateTime();
                foreach (CloudTask task in stateChangedTasks)
                {
                    //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitor] task {0} state changed to {1}, at date time = {2}\n", task.Id, task.State, task.StateTransitionTime);
                    TaskState state = task.State.Value;
                    DateTime stateTransitionTime = task.StateTransitionTime.Value;
                    if (state == TaskState.Running)
                    {
                        TaskInfo info = new TaskInfo();
                        info.Id = task.Id;
                        info.State = TaskStateConverter.FromAzureBatchTaskState(task.State.Value);
                        info.MachineName = nodes.First(n => n.AffinityId == task.ComputeNodeInformation.AffinityId)
                            .IPAddress;
                        info.Capacity = this.nodeCapacity;
                        //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitor] Node capacity in pool is\n", nodeCapacity);
                        results.Add(info);
                    }
                    else if (state == TaskState.Completed)
                    {
                        TaskInfo info = new TaskInfo
                        {
                            Id = task.Id,
                            State = TaskStateConverter.FromAzureBatchTaskState(task.State.Value, task.ExecutionInformation.ExitCode.Value)
                        };
                        results.Add(info);
                    }

                    if (DateTime.Compare(lastStateTransitionTime, stateTransitionTime) < 1)
                    {
                        lastStateTransitionTime = stateTransitionTime;
                    }
                }
                this.cloudJob.Refresh();
                this.lastChangeTime = lastStateTransitionTime;
                return results;
            }
            catch (Exception ex)
            {
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Warning, "[AzureBatchJobMonitor] Fail when get task info: {0}", ex);
                return null;
            }
            finally
            {
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Verbose, "[AzureBatchJobMonitor] Query task info finished.");
            }
        }

        /// <summary>
        /// Dispose the job monitor
        /// </summary>
        /// <param name="disposing">indicating the disposing flag</param>
        private void Dispose(bool disposing)
        {
            if (Interlocked.Increment(ref this.disposeFlag) == 1)
            {
                if (disposing)
                {
                    if (this.batchClient != null)
                    {
                        //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitor] Start to dispose batch client in AzureBatchJobMonitor.");
                        this.batchClient.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Dispose the job monitor entry
        /// </summary>
        void IDisposable.Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

    }
}
