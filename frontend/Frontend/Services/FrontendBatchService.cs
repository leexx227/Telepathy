using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Telepathy.Frontend.MessagePersist;
using NsqSharp;
using StackExchange.Redis;

namespace Microsoft.Telepathy.Frontend.Services
{
    using Microsoft.Telepathy.ProtoBuf;

    public class FrontendBatchService : FrontendBatch.FrontendBatchBase
    {
        public FrontendBatchService()
        {
        }

        public override async Task<Empty> EndTasks(ClientEndOfTaskRequest request, ServerCallContext context)
        {
            var channel = GrpcChannel.ForAddress(Configuration.SessionServiceAddress);
            var sessionSvcClient = new SessionManager.SessionManagerClient(channel);
            await sessionSvcClient.ClientEndOfTaskAsync(request);

            IDatabase cache = CommonUtility.Connection.GetDatabase();
            var topicName = GetTopicName(request.BatchClientInfo.SessionId, request.BatchClientInfo.ClientId) + ".totalNum";
            await cache.StringSetAsync(topicName, request.TotalRequestNumber);

            return new Empty();
        }

        public override async Task GetResults(BatchClientIdentity request, IServerStreamWriter<InnerResult> responseStream, ServerCallContext context)
        {
            using (IMessagePersist resultPersist = new RedisPersist(request))
            {
                while (!context.CancellationToken.IsCancellationRequested)
                {
                    var result = await resultPersist.GetResultAsync();

                    if (result != null)
                    {
                        await responseStream.WriteAsync(result);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            //IDatabase cache = CommonUtility.Connection.GetDatabase();
            //var topicName = GetTopicName(request.SessionId, request.ClientId);
            //int lastIndex = 0;

            //int totalNum = Int32.MaxValue;


            //Task.Run(() =>
            //{
            //    while (!cache.StringGet(topicName + ".totalNum").TryParse(out totalNum)) {
            //    }
            //});


            //while (!context.CancellationToken.IsCancellationRequested && lastIndex < totalNum)
            //{
            //    var list = cache.ListRange(topicName, lastIndex, lastIndex + Configuration.MaxCacheTasks - 1);

            //    foreach (var item in list)
            //    {
            //        var result = InnerResult.Parser.ParseFrom(item);
            //        await responseStream.WriteAsync(result);
            //    }

            //    lastIndex = lastIndex + list.Length;

            //    if (lastIndex < totalNum && list.Length < Configuration.MaxCacheTasks)
            //    {
            //        await Task.Delay(500);
            //    }
            //}

        }

        public override async Task<Empty> SendTask(IAsyncStreamReader<InnerTask> requestStream, ServerCallContext context)
        {
            using (IMessagePersist requestPersist = new NsqPersist())
            {
                await foreach (var request in requestStream.ReadAllAsync())
                {
                    await requestPersist.PutTaskAsync(request);
                }
            }

            return new Empty();
        }

        public override async Task<Empty> CloseBatch(CloseBatchClientRequest request, ServerCallContext context)
        {
            var channel = GrpcChannel.ForAddress(Configuration.SessionServiceAddress);
            var sessionSvcClient = new SessionManager.SessionManagerClient(channel);
            await sessionSvcClient.CloseBatchClientAsync(request);

            return new Empty();
        }

        private static string GetTopicName(string sessionId, string clientId)
        {
            var sb = new StringBuilder(sessionId);
            sb.Append('.');
            sb.Append(clientId);
            return sb.ToString();
        }
    }
}
