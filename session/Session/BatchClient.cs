// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Session
{
    public class BatchClient
    {
        public string SessionId { get; }
        public string ClientId { get; }
        public BatchClientState State { set; get; }

        public BatchClient(string sessionId, string clientId)
        {
            this.SessionId = sessionId;
            this.ClientId = clientId;
            this.State = BatchClientState.Active;
        }
    }
}
