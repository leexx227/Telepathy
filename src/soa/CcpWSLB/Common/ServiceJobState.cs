﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ServiceBroker.Common
{
    /// <summary>
    /// Service job state
    /// </summary>
    internal enum ServiceJobState
    {
        /// <summary>
        /// Not started
        /// </summary>
        NotStarted = 0,

        /// <summary>
        /// Service job is Started
        /// </summary>
        Started = 1,

        /// <summary>
        /// Service job is idle
        /// </summary>
        Idle = 2,

        /// <summary>
        /// Service job is busy
        /// </summary>
        Busy = 3,

        /// <summary>
        /// Service job is finished
        /// </summary>
        Finished = 4,
    }
}
