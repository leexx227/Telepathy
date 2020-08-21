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

        public override async Task<SessionReply> AttachSession(AttachSessionRequest request, ServerCallContext context)
        {
            var channel = GrpcChannel.ForAddress(Configuration.SessionServiceAddress);
            var sessionSvcClient = new SessionManager.SessionManagerClient(channel);
            var sessionInfo = await sessionSvcClient.AttachSessionAsync(request);

            return sessionInfo;
        }

        public override async Task<Empty> CloseSession(CloseSessionRequest request, ServerCallContext context)
        {
            var channel = GrpcChannel.ForAddress(Configuration.SessionServiceAddress);
            var sessionSvcClient = new SessionManager.SessionManagerClient(channel);
            await sessionSvcClient.CloseSessionAsync(request);

            return new Empty();
        }

        public override async Task<SessionReply> CreateSession(CreateSessionRequest request, ServerCallContext context)
        {
            Console.WriteLine("Create " + Configuration.SessionServiceAddress);
            var channel = GrpcChannel.ForAddress(Configuration.SessionServiceAddress);
            var sessionSvcClient = new SessionManager.SessionManagerClient(channel);
            var sessionInfo = await sessionSvcClient.CreateSessionAsync(request);

            return sessionInfo;
        }

        public override async Task<CreateBatchClientReply> CreateBatchClient(CreateBatchClientRequest request, ServerCallContext context)
        {
            var channel = GrpcChannel.ForAddress(Configuration.SessionServiceAddress);
            var sessionSvcClient = new SessionManager.SessionManagerClient(channel);
            var result = await sessionSvcClient.CreateBatchClientAsync(request);

            return result;
        }
    }
}
