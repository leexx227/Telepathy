// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace Microsoft.Telepathy.HostAgent.UnitTest
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Grpc.Core;

    using Microsoft.Telepathy.ProtoBuf;

    public class FakeService : Echo.EchoBase
    {
        public static Server GetService(int port, object serviceImpl)
        {
            Server server = new Server();
            Type t = serviceImpl.GetType();
            if (t.Equals(typeof(CorrectService)))
            {
                server = new Server
                {
                    Services = { Microsoft.Telepathy.ProtoBuf.Echo.BindService(new CorrectService()) },
                    Ports = { new ServerPort("0.0.0.0", port, ServerCredentials.Insecure) }
                };
            }
            else if (t.Equals(typeof(ExceptionService)))
            {
                server = new Server
                {
                    Services = { Microsoft.Telepathy.ProtoBuf.Echo.BindService(new ExceptionService()) },
                    Ports = { new ServerPort("0.0.0.0", port, ServerCredentials.Insecure) }
                };
            }

            return server;
        }

        public class CorrectService : Echo.EchoBase
        {
            public override async Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            {
                await Task.Delay(request.DelayTime);
                return new EchoReply
                {
                    Message = request.Message
                };
            }
        }

        public class ExceptionService : Echo.EchoBase
        {
            public override async Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
            {
                throw new RpcException(new Status(StatusCode.Internal, ""));
            }
        }
    }
}
