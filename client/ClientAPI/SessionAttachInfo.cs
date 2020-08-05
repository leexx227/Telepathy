using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Telepathy.ClientAPI
{
    public class SessionAttachInfo
    {
        public string SessionId => sessionId;

        private string sessionId;

        public string TelepathyAddress => telepathyAddress;

        private string telepathyAddress;

        public SessionAttachInfo(string sessionid, string address)
        {
            sessionId = sessionid;
            telepathyAddress = address;
        }
    }
}
