// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ResourceProvider.Impls.AzureBatch
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Telepathy.Session;

    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Common;
    using TaskState = Azure.Batch.Common.TaskState;

    public static class AzureBatchJobStateConverter
    {
        public static async Task<SessionState> FromAzureBatchJobAsync(CloudJob job)
        {
            //Handle job state Active independently in case of batch tasks are not running
            using (var batchClient = AzureBatchConfiguration.GetBatchClient())
            {
                if (job.State == JobState.Active)
                {
                    ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id,state");
                    List<CloudTask> allTasks = await batchClient.JobOperations.ListTasks(job.Id, detail).ToListAsync();
                    if (allTasks.Exists(task => task.State == TaskState.Running))
                    {
                        return SessionState.Running;
                    }
                    return SessionState.Queued;
                }
                else if (job.State == JobState.Terminating)
                {
                    ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id,state");
                    List<CloudTask> allTasks = await batchClient.JobOperations.ListTasks(job.Id, detail).ToListAsync();
                    if (allTasks.Exists(task => task.State != TaskState.Completed))
                    {
                        return SessionState.Canceling;
                    }
                    return SessionState.Finishing;
                }
            }
            return JobStateMapping[job.State.Value];
        }

        private static readonly Dictionary<JobState, SessionState> JobStateMapping = new Dictionary<JobState, SessionState>
        {
            { JobState.Completed, SessionState.Finished},
            { JobState.Deleting, SessionState.Canceling},
            { JobState.Disabled, SessionState.Queued},
            { JobState.Disabling, SessionState.Running},
            { JobState.Enabling, SessionState.Creating}
        };
    }
}
