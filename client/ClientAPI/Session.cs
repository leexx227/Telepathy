using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Telepathy.ProtoBuf;

namespace Microsoft.Telepathy.ClientAPI
{
    public class Session : IDisposable
    {
        private SessionReply sessionInfo;

        public SessionReply SessionInfo => sessionInfo;

        private string telepathyAddress;

        public string TelepathyAddress => telepathyAddress;

        public string Id => sessionInfo.SessionId;

        public Session(SessionReply sessionInfo, string address)
        {
            this.sessionInfo = sessionInfo;
            telepathyAddress = address;
        }


        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        public async Task CloseAsync()
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            var channel = GrpcChannel.ForAddress(telepathyAddress, new GrpcChannelOptions() { HttpHandler = httpClientHandler });
            var client = new FrontendSession.FrontendSessionClient(channel);

            //TODO auth
            //var metadata = new Metadata();
            //metadata.Add("Authorization", $"Bearer {tokenResponse.AccessToken}");

            await client.CloseSessionAsync(new CloseSessionRequest{ SessionId = sessionInfo.SessionId});

            this.Dispose();
        }

        public async Task CreateSessionClientAsync(BatchClientIdentity batchInfo)
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            var channel = GrpcChannel.ForAddress(telepathyAddress, new GrpcChannelOptions() { HttpHandler = httpClientHandler });

            var client = new FrontendSession.FrontendSessionClient(channel);
            await client.CreateBatchClientAsync(new CreateBatchClientRequest(){BatchClientInfo = batchInfo});
        }

        public static async Task<Session> CreateSessionAsync(SessionStartInfo sessionStartInfo)
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            var channel = GrpcChannel.ForAddress(sessionStartInfo.TelepathyAddress, new GrpcChannelOptions() { HttpHandler = httpClientHandler });

            var client = new FrontendSession.FrontendSessionClient(channel);
            var result = await client.CreateSessionAsync(new CreateSessionRequest
            {
                SessionInitInfo = new SessionInitInfo
                {
                    ServiceName = sessionStartInfo.ServiceName, MaxServiceInstance = sessionStartInfo.MaxServiceNum,
                    ServiceVersion = sessionStartInfo.ServiceVersion != null
                        ? sessionStartInfo.ServiceVersion.ToString()
                        : string.Empty,
                    ClientIdleTimeout = sessionStartInfo.ClientIdleTimeout,
                    SessionIdleTimeout = sessionStartInfo.SessionIdleTimeout
                }
            });
            return new Session(result, sessionStartInfo.TelepathyAddress);
        }

        public static async Task<Session> AttachSessionAsync(SessionAttachInfo sessionAttachInfo)
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            var channel = GrpcChannel.ForAddress(sessionAttachInfo.TelepathyAddress, new GrpcChannelOptions() { HttpHandler = httpClientHandler });
            var client = new FrontendSession.FrontendSessionClient(channel);
            var result = await client.AttachSessionAsync(new AttachSessionRequest{ SessionId = sessionAttachInfo.SessionId});
            return new Session(result, sessionAttachInfo.TelepathyAddress);
        }

        public static async Task CloseSessionAsync(string sessionId, string address)
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions() { HttpHandler = httpClientHandler });
            var client = new FrontendSession.FrontendSessionClient(channel);
            await client.CloseSessionAsync(new CloseSessionRequest { SessionId = sessionId });
        }
    }
}
