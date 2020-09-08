// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System.Collections.Generic;
using Microsoft.Telepathy.Common;
using Microsoft.Telepathy.ResourceProvider.SessionLauncher;

namespace Microsoft.Telepathy.SessionManager.Services
{
    using System;
    using System.Threading.Tasks;
    using Grpc.Core;
    using System.Collections.Concurrent;
    using Microsoft.Extensions.Logging;
    using Microsoft.Telepathy.ProtoBuf;
    using Microsoft.Telepathy.Session;
    using Google.Protobuf.WellKnownTypes;
    using Grpc.Net.Client;
    using SessionInitInfo = Microsoft.Telepathy.Session.SessionInitInfo;
    using Microsoft.Telepathy.ResourceProvider;
    using Microsoft.Telepathy.ResourceProvider.Impls.AzureBatch;
    using Microsoft.Telepathy.ResourceProvider.Impls.AzureBatch.SchedulerDelegations;
    using QueueManager.NsqMonitor;

    public class SessionService : SessionManager.SessionManagerBase
    {
        private readonly ILogger<SessionService> _logger;

        private readonly ResourceProvider _resourceProvider;

        private readonly AzureBatchSchedulerDelegation _schedulerDelegation;

        private readonly NsqDelegation _nsqDelegation;

        private readonly ConcurrentDictionary<string, Session> _activeSessions;

        public SessionService(ILogger<SessionService> logger)
        {
            AzureBatchSessionConfiguration sessionConfiguration = SessionConfigurationManager.ConfigureAzureBatchSessionFromJsonFile(@"C:\Users\jingjli\Desktop\AzureBatch.json");
            ResourceProviderRuntimeConfiguration.SessionLauncherStorageConnectionString = sessionConfiguration.SoaStorageConnectionString;
            AzureBatchConfiguration.InitializeAzureBatchConfiguration(sessionConfiguration);
            _logger = logger;
            _resourceProvider = new AzureBatchResourceProvider();
            _schedulerDelegation = new AzureBatchSchedulerDelegation(_resourceProvider);
            _nsqDelegation = new NsqDelegation();
            _activeSessions = new ConcurrentDictionary<string, Session>();
        }

        public override async Task<SessionReply> CreateSession(CreateSessionRequest createSessionRequest, ServerCallContext context)
        {
            _logger.LogInformation("Start to create session...");
            
            Version serviceVersion = string.IsNullOrEmpty(createSessionRequest.SessionInitInfo.ServiceVersion) ? null : Version.Parse(createSessionRequest.SessionInitInfo.ServiceVersion);
            SessionInitInfo info = new SessionInitInfo(createSessionRequest.SessionInitInfo.ServiceName, serviceVersion, createSessionRequest.SessionInitInfo.Durable, createSessionRequest.SessionInitInfo.MaxServiceInstance, createSessionRequest.SessionInitInfo.SessionIdleTimeout, createSessionRequest.SessionInitInfo.ClientIdleTimeout, createSessionRequest.SessionInitInfo.SessionCreator);
            Session session = new Session(info);
            //create a session job and ask for resource from resource provider
            ResourceAllocateInfo resourceAllocateInfo = await _resourceProvider.AllocateSessionResourceAsync(session);
            session.SessionId = resourceAllocateInfo.Id;
            Console.WriteLine($"Current session id is {session.SessionId}.");
            _activeSessions.TryAdd(session.SessionId, session);
            session.UpdateSessionState(session.SessionId, SessionState.Creating);
            //Register to start session job monitor which maintains the session state
            await _schedulerDelegation.RegisterJobAsync(session.SessionId);
            return new SessionReply { SessionId = resourceAllocateInfo.Id };
        }

        public override Task<SessionReply> AttachSession(AttachSessionRequest request, ServerCallContext context)
        {
            return Task.FromResult(new SessionReply { SessionId = "test" });
        }

        public override Task<Empty> CloseSession(CloseSessionRequest request, ServerCallContext context)
        {
            string sessionId = request.SessionId;
            _resourceProvider.TerminateAsync(sessionId);
            //TODO: Clean up session client queues
            _activeSessions.TryRemove(sessionId, out Session session);
            return Task.FromResult(new Empty());
        }

        public override Task<CreateBatchClientReply> CreateBatchClient(CreateBatchClientRequest request, ServerCallContext context)
        {
            string sessionId = request.BatchClientInfo.SessionId;
            string clientId = request.BatchClientInfo.ClientId;
            _activeSessions.TryGetValue(sessionId, out Session session);
            session?.AddBatchClient(clientId);
            int clientTimeout = session.SessionInitInfo.ClientIdleTimeout;
            _nsqDelegation.RegisterBatchClient(sessionId,clientId, clientTimeout);
            return Task.FromResult(new CreateBatchClientReply{ IsReady = true});
        }

        public override Task<Empty> CloseBatchClient(CloseBatchClientRequest request, ServerCallContext context)
        {
            string sessionId = request.BatchClientInfo.SessionId;
            string clientId = request.BatchClientInfo.ClientId;
            _activeSessions.TryGetValue(sessionId, out Session session);
            session?.RemoveBatchClient(clientId);
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> ClientEndOfTask(ClientEndOfTaskRequest request, ServerCallContext context)
        {
            string sessionId = request.BatchClientInfo.SessionId;
            string clientId = request.BatchClientInfo.ClientId;
            _activeSessions.TryGetValue(sessionId, out Session session);
            BatchClient client = null;
            session?.UpdateBatchClientState(clientId, BatchClientState.EndOfRequest);
            return Task.FromResult(new Empty());
        }
    }
}
