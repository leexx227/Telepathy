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

        public SessionService(ILogger<SessionService> logger)
        {
            
            var sessionConfigFilePath = Environment.GetEnvironmentVariable(SessionConstants.SessionConfigPathEnvVar);
            AzureBatchSessionConfiguration sessionConfiguration = SessionConfigurationManager.ConfigureAzureBatchSessionFromJsonFile(sessionConfigFilePath);
            ResourceProviderRuntimeConfiguration.SessionLauncherStorageConnectionString = sessionConfiguration.SoaStorageConnectionString;
            AzureBatchConfiguration.InitializeAzureBatchConfiguration(sessionConfiguration);
            _logger = logger;
            _resourceProvider = new AzureBatchResourceProvider();
            _schedulerDelegation = new AzureBatchSchedulerDelegation(_resourceProvider);
            _nsqDelegation = new NsqDelegation();
        }

        public override async Task<SessionReply> CreateSession(CreateSessionRequest createSessionRequest, ServerCallContext context)
        {
            _logger.LogInformation("Start to create session...");
            Version serviceVersion = string.IsNullOrEmpty(createSessionRequest.SessionInitInfo.ServiceVersion) ? null : Version.Parse(createSessionRequest.SessionInitInfo.ServiceVersion);
            SessionInitInfo info = new SessionInitInfo(createSessionRequest.SessionInitInfo.ServiceName, serviceVersion, createSessionRequest.SessionInitInfo.Durable, createSessionRequest.SessionInitInfo.MaxServiceInstance, createSessionRequest.SessionInitInfo.SessionCreator);
            //create a session job and ask for resource from resource provider
            ResourceAllocateInfo resourceAllocateInfo = await _resourceProvider.AllocateSessionResourceAsync(info);
            Console.WriteLine($"Current session id is {resourceAllocateInfo.Id}.");
            Session.RegisterSessionInfo(resourceAllocateInfo.Id, info);
            Session.UpdateSessionState(resourceAllocateInfo.Id, SessionState.Creating);
            //Register to start session job monitor which maintains the session state
            await _schedulerDelegation.RegisterJobAsync(resourceAllocateInfo.Id);
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
            //Do cleanup work, set all batchclient state as closed
            Session.CloseAllBatchClients(sessionId);
            return Task.FromResult(new Empty());
        }

        public override async Task<CreateBatchClientReply> CreateBatchClient(CreateBatchClientRequest request, ServerCallContext context)
        {
            string sessionId = request.BatchClientInfo.SessionId;
            string clientId = request.BatchClientInfo.ClientId;
            int clientTimeout = Session.AddBatchClient(sessionId, clientId);
           
            await _nsqDelegation.RegisterBatchClientAsync(sessionId, clientId, clientTimeout);
            return new CreateBatchClientReply{ IsReady = true};
        }

        public override Task<Empty> CloseBatchClient(CloseBatchClientRequest request, ServerCallContext context)
        {
            string sessionId = request.BatchClientInfo.SessionId;
            string clientId = request.BatchClientInfo.ClientId;
            Session.UpdateBatchClientState(sessionId, clientId, BatchClientState.Closed);
            Session.RemoveBatchClient(sessionId, clientId);
            return Task.FromResult(new Empty());
        }

        public override Task<Empty> ClientEndOfTask(ClientEndOfTaskRequest request, ServerCallContext context)
        {
            string sessionId = request.BatchClientInfo.SessionId;
            string clientId = request.BatchClientInfo.ClientId;
            Console.WriteLine($"total request number is {request.TotalRequestNumber}");
            Session.UpdateBatchClientState(sessionId, clientId, BatchClientState.EndOfRequest, requestNum: request.TotalRequestNumber);
            return Task.FromResult(new Empty());
        }
    }
}
