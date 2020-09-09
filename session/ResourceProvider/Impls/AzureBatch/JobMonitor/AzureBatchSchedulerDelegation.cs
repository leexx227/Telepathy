// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ResourceProvider.Impls.AzureBatch.SchedulerDelegations
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Microsoft.Azure.Batch;

    using JobState = Session.SessionState;

    public class AzureBatchSchedulerDelegation : ISchedulerAdapter
    {
        public AzureBatchSchedulerDelegation(ResourceProvider instance)
        {
            this._sessionLauncher = instance;
            Trace.TraceInformation("[AzureBatchSchedulerDelegation] Successfully initialized scheduler adapter.");
        }

        private readonly ResourceProvider _sessionLauncher;

        /// <summary>
        /// The dictionary to store the monitors: (JobId, JobMonitorEntry)
        /// </summary>
        private readonly Dictionary<string, AzureBatchJobMonitorEntry> _jobMonitors = new Dictionary<string, AzureBatchJobMonitorEntry>();

        public async Task<bool> FinishTaskAsync(string jobId, string taskUniqueId)
        {
            Trace.TraceWarning($"Ignored call to {nameof(this.FinishTaskAsync)}");
            return true;
        }

        public async Task<bool> ExcludeNodeAsync(string jobid, string nodeName)
        {
            Trace.TraceWarning($"Ignored call to {nameof(this.ExcludeNodeAsync)}");

            return true;
        }

        public async Task RequeueOrFailJobAsync(string sessionId, string reason)
        {
            Trace.TraceWarning($"Ignored call to {nameof(this.RequeueOrFailJobAsync)}");
        }
        public async Task FailJobAsync(string sessionId, string reason) => await this._sessionLauncher.TerminateAsync(sessionId);

        public async Task FinishJobAsync(string sessionId, string reason) => await this._sessionLauncher.TerminateAsync(sessionId);

        /// <summary>
        /// Start to subscribe the job and task event
        /// </summary>
        /// <param name="jobid">indicating the job id</param>
        /// <param name="autoMax">indicating the auto max property of the job</param>
        /// <param name="autoMin">indicating the auto min property of the job</param>
        public async Task<(JobState jobState, int autoMax, int autoMin)> RegisterJobAsync(string jobid)
        {
            Trace.TraceInformation($"[AzureBatchSchedulerDelegation] Begin: RegisterJob, job id is {jobid}...");
            //CheckBrokerAccess(jobid);

            int autoMax = 0, autoMin = 0;
            CloudJob batchJob;
            try
            {
                AzureBatchJobMonitorEntry jobMonitorEntry;
                lock (this._jobMonitors)
                {
                    if (!this._jobMonitors.TryGetValue(jobid, out jobMonitorEntry))
                    {
                        jobMonitorEntry = new AzureBatchJobMonitorEntry(jobid);
                        jobMonitorEntry.Exit += new EventHandler(this.JobMonitorEntry_Exit);
                    }
                }

                batchJob = await jobMonitorEntry.StartAsync();

                // Bug 18050: Only add/update the instance if it succeeded to
                // open the job.
                lock (this._jobMonitors)
                {
                    this._jobMonitors[jobid] = jobMonitorEntry;
                }

                //autoMin = jobMonitorEntry.MinUnits;
                autoMax = jobMonitorEntry.MaxServiceInstance;
            }
            catch (Exception e)
            {
                Trace.TraceError($"[AzureBatchSchedulerDelegation] Exception thrown while registering job: {jobid}", e);
                throw;
            }

            Trace.TraceInformation($"[AzureBatchSchedulerDelegation] End: RegisterJob. Current job state = {batchJob.State}.");
            return (await AzureBatchJobStateConverter.FromAzureBatchJobAsync(batchJob), autoMax, autoMin);
        }

        /// <summary>
        /// Job finished event handler
        /// </summary>
        public static event EventHandler OnJobFinished;

        /// <summary>
        /// Job failed/canceled event handler
        /// </summary>
        public static event EventHandler OnJobFailedOrCanceled;


        /// <summary>
        /// Event triggered when job monitor entry's instance is exited
        /// </summary>
        /// <param name="sender">indicating the sender</param>
        /// <param name="e">indicating the event args</param>
        private async void JobMonitorEntry_Exit(object sender, EventArgs e)
        {
            AzureBatchJobMonitorEntry entry = (AzureBatchJobMonitorEntry)sender;
            //Determine if job exits under error condition
            JobState exitSessionState = entry.PreviousState;
            string sessionId = entry.SessionId;
            Console.WriteLine($"[AzureBatchSchedulerDelegation] {sessionId} : JobMonitorEntry_Exit, Exit session state is {exitSessionState}");
            switch (exitSessionState)
            {
                case JobState.Completed: 
                    await FinishJobAsync(sessionId, "Job finishes successfully.");
                    break;
                case JobState.Canceled:
                    await FinishJobAsync(sessionId, "Job is cancelled.");
                    break;
                case JobState.Failed:
                    await FailJobAsync(sessionId, "Job is failed.");
                    break;
            }
            Debug.Assert(entry != null, "[AzureBatchSchedulerDelegation] Sender should be an instance of JobMonitorEntry class.");
            lock (this._jobMonitors)
            {
                this._jobMonitors.Remove(sessionId);
            }

            entry.Exit -= new EventHandler(this.JobMonitorEntry_Exit);
            entry.Close();
            Trace.TraceInformation($"[AzureBatchSchedulerDelegation] End: JobMonitorEntry Exit.");
        }

        public Task<int?> GetTaskErrorCode(string jobId, string globalTaskId)
        {
            throw new NotImplementedException();
        }

        public Task<bool> UpdateSessionInfoAsync(string sessionId, Dictionary<string, object> properties)
        {
            throw new NotImplementedException();
        }

        public Task RequeueOrFailSessionAsync(string sessionId, string reason)
        {
            throw new NotImplementedException();
        }

        public Task FailSessionAsync(string sessionId, string reason)
        {
            throw new NotImplementedException();
        }

        public Task FinishSessionAsync(string sessionId, string reason)
        {
            throw new NotImplementedException();
        }
    }
}
