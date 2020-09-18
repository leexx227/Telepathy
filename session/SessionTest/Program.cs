// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace SessionTest
{
    using System;
    using Microsoft.Telepathy.ProtoBuf;
    using Grpc.Net.Client;

    class Program
    {
        private static GrpcChannel channel = GrpcChannel.ForAddress("https://localhost:5001");
        static void Main(string[] args)
        {
            SessionManager.SessionManagerClient sessionClient = new SessionManager.SessionManagerClient(channel);
            SessionInitInfo initInfo = new SessionInitInfo(){ServiceName = "echo", ServiceVersion = "", MaxServiceInstance = 5, SessionCreator = "Jingjing", Durable = true, ClientIdleTimeout = 10000, SessionIdleTimeout = 10000};
            var result = sessionClient.CreateSession(new CreateSessionRequest(){SessionInitInfo = initInfo});
            Console.WriteLine(result.SessionId);

            var batchClientIdentity = new BatchClientIdentity() { SessionId = result.SessionId, ClientId = "test" };
            var batchClientResult = sessionClient.CreateBatchClient(new CreateBatchClientRequest() { BatchClientInfo = batchClientIdentity });
            Console.WriteLine(batchClientResult);
        }
    }
}
