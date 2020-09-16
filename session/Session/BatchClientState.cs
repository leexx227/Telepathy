// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Session
{
    using System;

    /// <summary>
    /// Define Session client states
    /// </summary>
    [Serializable]
    public enum BatchClientState
    {
        /// <summary>
        /// The session client is active
        /// </summary>
        Active,

        /// <summary>
        /// The session client have send EOF
        /// </summary>
        EndOfRequest,

        /// <summary>
        /// The session client's requests have all been handled and all the responses have all been returned
        /// </summary>
        EndOfResponse,

        /// <summary>
        /// The session client is timeout to send requests
        /// </summary>
        Timeout,

        /// <summary>
        /// The session client is closed by user
        /// </summary>
        Closed
    }
}