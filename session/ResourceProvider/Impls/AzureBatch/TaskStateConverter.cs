// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ResourceProvider.Impls.AzureBatch
{
    using SessionTaskState = Session.TaskState;
    using TaskState = Azure.Batch.Common.TaskState;
    using System.Collections.Generic;

    public static class TaskStateConverter
    {
        public static SessionTaskState FromAzureBatchTaskState(TaskState state)
        {
            return TaskStateMapping[state];
        }

        public static SessionTaskState FromAzureBatchTaskState(TaskState state, string executionResult)
        {
            if (executionResult == "Failure")
            {
                return SessionTaskState.Failed;
            }

            return TaskStateMapping[state];
        }

        private static Dictionary<TaskState, SessionTaskState> TaskStateMapping = new Dictionary<TaskState, SessionTaskState>
        {
            { TaskState.Active, SessionTaskState.Creating },
            { TaskState.Completed, SessionTaskState.Finished },
            { TaskState.Preparing, SessionTaskState.Creating },
            { TaskState.Running, SessionTaskState.Running }
        };
    }
}
