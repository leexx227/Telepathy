// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.IdentityUtil
{
    using System;
    using System.ServiceModel.Channels;
    using System.Xml;

    public class AuthMessageHeader : MessageHeader
    {
        public static readonly string CustomHeaderName = "Authentication";
        public static readonly string CustomHeaderNamespace = "Telepathy";
        public string CustomData { get; private set; }
        public AuthMessageHeader(string customData)
        {
            this.CustomData = customData;
        }
        public override string Name
        {
            get { return CustomHeaderName; }
        }

        public override string Namespace
        {
            get { return CustomHeaderNamespace; }
        }

        protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
        {
            writer.WriteElementString(CustomHeaderName, CustomHeaderNamespace, this.CustomData);
        }

        public static string ReadHeader(Message request)
        {
            Int32 headerPosition = request.Headers.FindHeader(CustomHeaderName, CustomHeaderNamespace);
            if (headerPosition == -1)
            {
                return null;
            }

            MessageHeaderInfo headerInfo = request.Headers[headerPosition];
            XmlNode[] content = request.Headers.GetHeader<XmlNode[]>(headerPosition);
            return content[0].InnerText;
        }
    }
}
