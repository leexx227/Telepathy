using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Microsoft.Telepathy.ProtoBuf;

namespace Microsoft.Telepathy.ClientAPI
{
    public class Session : IDisposable
    {
        private ProtoBuf.SessionInfo sessionInfo;

        public ProtoBuf.SessionInfo SessionInfo => sessionInfo;

        private string telepathyAddress;

        public string TelepathyAddress => telepathyAddress;

        Session(ProtoBuf.SessionInfo sessionInfo, string address)
        {
            this.sessionInfo = sessionInfo;
            telepathyAddress = address;
        }


        public void Dispose()
        {
        }

        public async Task Close()
        {
            var channel = GrpcChannel.ForAddress(telepathyAddress);
            var client = new FrontendSession.FrontendSessionClient(channel);
            await client.CloseSessionAsync(new SessionId { Id = sessionInfo.Id });

            this.Dispose();
        }

        public async Task CreateSessionClient()
        {
            var channel = GrpcChannel.ForAddress(telepathyAddress);
            var client = new FrontendSession.FrontendSessionClient(channel);
            await client.CreateSessionClientAsync(new SessionClientInfo(){});
        }

        public static async Task<Session> CreateSession(SessionStartInfo sessionStartInfo)
        {
            var channel = GrpcChannel.ForAddress(sessionStartInfo.TelepathyAddress);

            var client = new FrontendSession.FrontendSessionClient(channel);
            var result = await client.CreateSessionAsync(new SessionInitInfo());
            return  new Session(result, sessionStartInfo.TelepathyAddress);
        }

        public static async Task<Session> AttachSession(SessionAttachInfo sessionAttachInfo)
        {
            var channel = GrpcChannel.ForAddress(sessionAttachInfo.TelepathyAddress);
            var client = new FrontendSession.FrontendSessionClient(channel);
            var result = await client.AttachSessionAsync(new SessionId {Id = sessionAttachInfo.SessionId});
            return new Session(result, sessionAttachInfo.TelepathyAddress);
        }

        public static async Task CloseSession(string sessionId, string address)
        {
            var channel = GrpcChannel.ForAddress(address);
            var client = new FrontendSession.FrontendSessionClient(channel);
            await client.CloseSessionAsync(new SessionId { Id = sessionId });
        }
    }
}
