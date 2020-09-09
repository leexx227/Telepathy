// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using StackExchange.Redis;

namespace Microsoft.Telepathy.ResourceProvider.Impls.AzureBatch
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Batch;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Telepathy.Common;
    using Session;
    using BatchClient = Azure.Batch.BatchClient;

    internal class AzureBatchJobMonitorEntry : IDisposable
    {
        /// <summary>
        /// Stores the dispose flag
        /// </summary>
        private int disposeFlag;

        public int MaxServiceInstance { get; private set; }

        /// <summary>
        /// Stores the date time that Start() opertaion was last called
        /// </summary>
        private DateTime lastStartTime = DateTime.MinValue;

        /// <summary>
        /// Bacth Client configuration
        /// </summary>
        private readonly BatchClient _batchClient;

        /// <summary>
        /// monitor Azure Batch job state
        /// </summary>
        private AzureBatchJobMonitor batchJobMonitor;

        /// <summary>
        /// Gets or sets the exit event
        /// </summary>
        public event EventHandler Exit;

        /// <summary>
        /// Lock for check job state
        /// </summary>
        private object changeJobStateLock = new object();

        private readonly IDatabase _cache;

        /// <summary>
        /// Initializes a new instance of the JobMonitorEntry class
        /// </summary>
        /// <param name="sessionid">indicating the session id</param>
        public AzureBatchJobMonitorEntry(string sessionId)
        {
            this.SessionId = sessionId;
            this._batchClient = AzureBatchConfiguration.GetBatchClient();
            this._cache = RedisDB.Connection.GetDatabase();
        }

        /// <summary>
        /// Gets the previous state
        /// </summary>
        public SessionState PreviousState { get; private set; }

        /// <summary>
        /// Gets the session id
        /// </summary>
        public string SessionId { get; }

        /// <summary>
        /// Gets the service job
        /// </summary>
        internal CloudJob CloudJob { get; private set; }

        /// <summary>
        /// Dispose the job monitor entry
        /// </summary>
        void IDisposable.Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Start the monitor
        /// </summary>
        public async Task<CloudJob> StartAsync()
        {
            //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitorEntry] Start monitor Entry.");
            this.PreviousState = SessionState.Queued;
            this.CloudJob = await this._batchClient.JobOperations.GetJobAsync(AzureBatchSessionJobIdConverter.ConvertToAzureBatchJobId(this.SessionId));

            if (this.CloudJob.State == JobState.Disabled)
            {
                //ThrowHelper.ThrowSessionFault(SOAFaultCode.Session_ValidateJobFailed_JobCanceled, SR.SessionLauncher_ValidateJobFailed_JobCanceled, this.sessionid.ToString());
                throw new Exception("Current session job is disabled in AzureBatch!");
            }

            if (this.CloudJob.Metadata != null)
            {
                MetadataItem maxServiceInstanceItem = this.CloudJob.Metadata.FirstOrDefault(item => item.Name == SessionSettingsConstants.ServiceInstanceNumber);
                if (maxServiceInstanceItem != null)
                {
                    if (Int32.TryParse(maxServiceInstanceItem.Value, out var result))
                    {
                        this.MaxServiceInstance = result;
                    }
                }
            }

            // monitor batch job state
            this.batchJobMonitor = new AzureBatchJobMonitor(this.SessionId, this.JobMonitor_OnReportJobState);
            try
            {
                Task.Run(() => StartMonitorAsync());
            }
            catch (Exception e)
            {
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Warning, "[AzureBatchJobMonitorEntry] Exception thrown when start Azure Batch Job Monitor: {0}", e);
                throw e;
            }

            return this.CloudJob;
        }

        /// <summary>
        /// Close the job monitor entry
        /// </summary>
        public void Close()
        {
            this.Dispose(true);
        }

        /// <summary>
        /// Query task info
        /// </summary>
        private async Task StartMonitorAsync()
        {
            try
            {
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Verbose, "[AzureBatchJobMonitorEntry] Start Azure Batch Job Monitor.");
                RetryManager retry = new RetryManager(new ExponentialBackoffRetryTimer(2000, 30000), 5);
                await RetryHelper<Task>.InvokeOperationAsync(
                        async () =>
                        {
                            await this.batchJobMonitor.StartAsync();
                            return null;
                        },
                        async (e, r) => await Task.FromResult<object>(new Func<object>(() =>
                        {
                            //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Warning, "[AzureBatchJobMonitorEntry] Exception thrown when trigger start Azure Batch Job Monitor: {0} ", e, r.RetryCount); 
                            return null;
                        }).Invoke()),
                        retry);
            }
            catch (Exception e)
            {
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Warning, "[AzureBatchJobMonitorEntry] Exception thrown when trigger start Azure Batch Job Monitor: {0}", e);
            }
        }

        /// <summary>
        /// Callback when Azure Batch Monitor report jon state
        /// </summary>
        private async void JobMonitor_OnReportJobState(SessionState state, List<TaskInfo> stateChangedTaskList, bool shouldExit)
        {
            if (state != this.PreviousState)
            {
                lock (this.changeJobStateLock)
                {
                    if (state != this.PreviousState)
                    {
                        this.PreviousState = state;
                        Console.WriteLine($"[AzureBatchJobMonitorEntry] JobMonitor_OnReportJObState {SessionId} : update session state to {PreviousState}");
                        // Change the session state in Redis
                        _cache.StringSet(SessionConfigurationManager.GetRedisSessionStateKey(SessionId), this.PreviousState.ToString());
                    }
                }
            }

            Console.WriteLine($"[AzureBatchJobMonitorEntry] JobMonitor_OnReportJObState {SessionId}  : Current reported job state is {PreviousState}, should exit is {shouldExit} .");

            if (shouldExit)
            {
                //TraceHelper.TraceEvent(this.sessionid, TraceEventType.Information, "[AzureBatchJobMonitorEntry] Exit AzureBatchJobMonitor Entry");
                this.Exit?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Dispose the job monitor entry
        /// </summary>
        /// <param name="disposing">indicating the disposing flag</param>
        private void Dispose(bool disposing)
        {
            if (Interlocked.Increment(ref this.disposeFlag) == 1)
            {
                if (disposing)
                {
                    if (this._batchClient != null)
                    {
                        this._batchClient.Dispose();
                    }
                }
            }
        }
    }
}
