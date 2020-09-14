// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace Microsoft.Telepathy.HostAgent.UnitTest
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Grpc.Core;

    using Microsoft.Telepathy.ProtoBuf;

    public class FakeDispatcher
    {

        public static Server GetDispatcher(int port, object dispathcherImpl)
        {
            Server server = new Server();
            System.Type t = dispathcherImpl.GetType();
            if (t.Equals(typeof(Normal)))
            {
                server = new Server
                {
                    Services = { Dispatcher.BindService(new Normal()) },
                    Ports = { new ServerPort("0.0.0.0", port, ServerCredentials.Insecure) }
                };
            }
            else if (t.Equals(typeof(TempNoTask)))
            {
                server = new Server
                {
                    Services = { Dispatcher.BindService(new TempNoTask()) },
                    Ports = { new ServerPort("0.0.0.0", port, ServerCredentials.Insecure) }
                };
            }
            else if (t.Equals(typeof(EndTask)))
            {
                server = new Server
                {
                    Services = { Dispatcher.BindService(new EndTask()) },
                    Ports = { new ServerPort("0.0.0.0", port, ServerCredentials.Insecure) }
                };
            }

            return server;
        }

        public class Normal : Dispatcher.DispatcherBase
        {
            public override Task<WrappedTask> GetWrappedTask(GetTaskRequest request, ServerCallContext context)
            {
                EchoRequest echoRequest = new EchoRequest() { Message = "hello", DelayTime = 0 };
                var md = Echo.Descriptor.FindMethodByName("Echo");
                InnerTask innerTask = new InnerTask() { ServiceName = md.Service.FullName, MethodName = md.Name, MethodType = MethodEnum.Unary, Msg = echoRequest.ToByteString() };
                WrappedTask newRequest = new WrappedTask { SerializedInnerTask = innerTask.ToByteString(), SessionState = SessionStateEnum.Running };
                return Task.FromResult(newRequest);
            }

            public override Task<Empty> SendResult(SendResultRequest request, ServerCallContext context)
            {
                return Task.FromResult(new Empty());
            }
        }

        public class TempNoTask : Dispatcher.DispatcherBase
        {
            public override Task<WrappedTask> GetWrappedTask(GetTaskRequest request, ServerCallContext context)
            {
                return Task.FromResult(new WrappedTask { SessionState = SessionStateEnum.TempNoTask });
            }
        }

        public class EndTask : Dispatcher.DispatcherBase
        {
            public override Task<WrappedTask> GetWrappedTask(GetTaskRequest request, ServerCallContext context)
            {
                return Task.FromResult(new WrappedTask { SessionState = SessionStateEnum.EndTask });
            }
        }
    }
}
