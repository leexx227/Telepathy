// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace Microsoft.Telepathy.HostAgent.Core
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Google.Protobuf;
    using Grpc.Core;

    public class MessageWrapper
    {
        public byte[] Msg;

        public MessageWrapper(ByteString input)
        {
            Msg = input.ToByteArray();
        }

        public MessageWrapper(byte[] input)
        {
            Msg = input;
        }

        public static byte[] Serialize(MessageWrapper req)
        {
            return req.Msg;
        }

        public static MessageWrapper Deserialize(byte[] bytes)
        {
            return new MessageWrapper(bytes);
        }
    }

    public class MethodWrapper
    {
        public readonly Method<MessageWrapper, MessageWrapper> Method;

        public MethodWrapper(string serviceName, string methodName, MethodType methodType)
        {
            Method = new Method<MessageWrapper, MessageWrapper>(
                methodType,
                serviceName,
                methodName,
                Marshallers.Create(MessageWrapper.Serialize, MessageWrapper.Deserialize),
                Marshallers.Create(MessageWrapper.Serialize, MessageWrapper.Deserialize));
        }

    }
}
