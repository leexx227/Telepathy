using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf;
using Grpc.Core;

namespace Microsoft.Telepathy.HostAgent.Core
{
    public class MessageWrapper
    {
        byte[] msg;

        public MessageWrapper(ByteString input)
        {
            msg = input.ToByteArray();
        }

        public MessageWrapper(byte[] input)
        {
            msg = input;
        }

        public static byte[] Serialize(MessageWrapper req)
        {
            return req.msg;
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
