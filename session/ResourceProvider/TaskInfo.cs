// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ResourceProvider
{
    using Microsoft.Telepathy.Session;

    public class TaskInfo
    {
        public string Id { set; get; }

        public int Capacity { set; get; }

        public TaskState State { set; get; }

        public string MachineName { set; get; }
    }
}
