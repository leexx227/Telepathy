// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.IdentityUtil
{
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.ServiceModel.Dispatcher;

    class IdentityServiceMessageInspector : IClientMessageInspector
    {
        private string authorization;

        public IdentityServiceMessageInspector(string jwtToken)
        {
            this.authorization = jwtToken;
        }

        public object BeforeSendRequest(ref Message request, IClientChannel channel)
        {
            MessageBuffer buffer = request.CreateBufferedCopy(int.MaxValue);
            request = buffer.CreateMessage();
            AuthMessageHeader header = new AuthMessageHeader(authorization);

            request.Headers.Add(header);
            return null;
        }

        public void AfterReceiveReply(ref Message reply, object correlationState)
        {
        }
    }
}
