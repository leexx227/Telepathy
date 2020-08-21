using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Telepathy.ProtoBuf;

namespace Microsoft.Telepathy.Frontend.Services
{
    public class FrontendSessionService : FrontendSession.FrontendSessionBase
    {

        public override async Task<SessionInfo> AttachSession(SessionId request, ServerCallContext context)
        {
            var channel = GrpcChannel.ForAddress(Configuration.SessionServiceAddress);
            var sessionSvcClient = new Session.SessionClient(channel);
            var sessionInfo = await sessionSvcClient.AttachSessionAsync(request);

            return sessionInfo;
        }

        public override async Task<Empty> CloseSession(SessionId request, ServerCallContext context)
        {
            var channel = GrpcChannel.ForAddress(Configuration.SessionServiceAddress);
            var sessionSvcClient = new Session.SessionClient(channel);
            await sessionSvcClient.CloseSessionAsync(request);

            return new Empty();
        }

        public override async Task<SessionInfo> CreateSession(SessionInitInfo request, ServerCallContext context)
        {
            Console.WriteLine("Create " + Configuration.SessionServiceAddress);
            var channel = GrpcChannel.ForAddress(Configuration.SessionServiceAddress);
            var sessionSvcClient = new Session.SessionClient(channel);
            var sessionInfo = await sessionSvcClient.CreateSessionAsync(request);

            return sessionInfo;
        }

        public override async Task<ClientQueueResult> CreateSessionClient(SessionClientInfo request, ServerCallContext context)
        {
            var channel = GrpcChannel.ForAddress(Configuration.SessionServiceAddress);
            var sessionSvcClient = new Session.SessionClient(channel);
            var result = await sessionSvcClient.CreateSessionClientQueuesAsync(request);

            return result;
        }
    }
}
