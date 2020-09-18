// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Session
{
    public class BatchClient
    {
        public string SessionId { get; }
        public string ClientId { get; }
        public BatchClientState State { set; get; }

        public static bool IsEndState(BatchClientState state)
        {
            return state == BatchClientState.Timeout || state == BatchClientState.Closed ||
                   state == BatchClientState.EndOfResponse;
        }

        public BatchClient(string sessionId, string clientId)
        {
            this.SessionId = sessionId;
            this.ClientId = clientId;
            this.State = BatchClientState.Active;
        }
    }
}
