using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Telepathy.ProtoBuf;
using Google.Protobuf.WellKnownTypes;

namespace Microsoft.Telepathy.ClientAPI
{

    public class BatchClient : IDisposable
    {
        private string clientId;

        public string ClientID
        {
            get => clientId;
            set => clientId = value;
        }

        private Session session;

        private MethodDescriptor method;

        private MethodEnum methodtype;

        private ConcurrentQueue<AsyncClientStreamingCall<InnerTask, Empty>> RequestCallQueue = new ConcurrentQueue<AsyncClientStreamingCall<InnerTask, Empty>>();

        private List<Lazy<FrontendBatch.FrontendBatchClient>> clients = new List<Lazy<FrontendBatch.FrontendBatchClient>>();

        private ConcurrentQueue<InnerTask> requestCache = new ConcurrentQueue<InnerTask>();

        private int requestCallLock = 0;

        private int connection;

        private int streamNum;

        private int totalCall = 0;

        private int requestCount = 0;

        private BatchClientIdentity batchInfo;

        private int index = 0;

        public BatchClient(Session session, MethodDescriptor method, int connection = 10, int streamNum = 2) : this(
            Guid.NewGuid().ToString(), session, method, connection, streamNum)
        {
        }

        public BatchClient(string clientId, Session session, MethodDescriptor method, int connection = 10, int streamNum = 2)
        {
            this.clientId = clientId;
            this.method = method;
            this.session = session;
            this.connection = connection;
            this.streamNum = streamNum;
            this.batchInfo = new BatchClientIdentity{ SessionId = session.Id, ClientId = clientId};

            if (method.IsClientStreaming)
            {
                methodtype = method.IsServerStreaming ? MethodEnum.DuplexStream : MethodEnum.ClientStream;
            }
            else
            {
                methodtype = method.IsServerStreaming ? MethodEnum.ServerStream : MethodEnum.Unary;
            }

            //TODO auth
            //var credentials = CallCredentials.FromInterceptor((context, metadata) =>
            //{
            //    if (!string.IsNullOrEmpty(tokenResponse.AccessToken))
            //    {
            //        metadata.Add("Authorization", $"Bearer {tokenResponse.AccessToken}");
            //    }
            //    return Task.CompletedTask;
            //});

            for (int i = 0; i < connection; i++)
            {
                var channel = GrpcChannel.ForAddress(session.TelepathyAddress);

                //TODO auth
                //var channel = GrpcChannel.ForAddress(session.TelepathyAddress, new GrpcChannelOptions
                //{
                //    Credentials = ChannelCredentials.Create(new SslCredentials(), credentials)
                //});

                var client = new FrontendBatch.FrontendBatchClient(channel);
                clients.Add(new Lazy<FrontendBatch.FrontendBatchClient>(() => new FrontendBatch.FrontendBatchClient(channel)));
            }

            session.CreateSessionClientAsync().GetAwaiter().GetResult();
        }

        public async Task CloseAsync()
        {
            var client = clients[GetNextClient()];
            await client.Value.CloseBatchAsync(new CloseBatchClientRequest {BatchClientInfo = batchInfo});
            this.Dispose();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private void Publish()
        {
            var temp = Interlocked.Exchange(ref requestCallLock, 1);
            if (temp == 0)
            {
                for (int i = 0; i < connection; i++)
                {
                    for (int j = 0; j < streamNum; j++)
                    {
                        RequestCallQueue.Enqueue(clients[i].Value.SendTask());
                    }
                }

                totalCall = RequestCallQueue.Count;
            }

            int count = 0;
            while (count < totalCall && RequestCallQueue.TryDequeue(out var callItem))
            {
                count++;
                var call = callItem;
                Task.Run(async () =>
                {
                    while (requestCache.TryDequeue(out var request))
                    {
                        await call.RequestStream.WriteAsync(request);
                    }

                    RequestCallQueue.Enqueue(call);
                });
            }
        }

        public void SendTask(IMessage request)
        {
            this.SendTask(request, Guid.NewGuid().ToString());
        }

        public void SendTask(IMessage request, string messageId)
        {
            if (request.GetType() == method.InputType.ClrType)
            {
                var inner = new InnerTask
                { ServiceName = method.Service.FullName, MethodName = method.Name, Msg = request.ToByteString(), MethodType = methodtype, MessageId = messageId, ClientId = clientId, SessionId = session.Id};

                requestCache.Enqueue(inner);
                Interlocked.Increment(ref requestCount);
                Publish();
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public async Task EndTasksAsync()
        {
            Console.WriteLine("Begin end of request");
            while (requestCache.Count > 0 || RequestCallQueue.Count < totalCall)
            {
                Console.WriteLine(requestCache.Count + " " + RequestCallQueue.Count);
                await Task.Delay(100);
            }

            Console.WriteLine("request cache is zero");
            foreach (var call in RequestCallQueue)
            {
                await call.RequestStream.CompleteAsync();
                await call.ResponseAsync;
            }

            await clients[GetNextClient()].Value.EndTasksAsync(new ClientEndOfTaskRequest{BatchClientInfo = batchInfo, TotalRequestNumber = requestCount});
        }

        public async Task<IEnumerable<TResponse>> GetResultsAsync<TResponse>() where TResponse : IMessage<TResponse>, new()
        {
            var call = clients[0].Value.GetResults(batchInfo);
            var result = new List<TResponse>();

            int faultCount = 0;
            var stringList = new List<string>();

            await foreach (var res in call.ResponseStream.ReadAllAsync())
            {
                if (res.StateCode == 0)
                {
                    var v = new MessageParser<TResponse>(() => new TResponse());
                    result.Add(v.ParseFrom(res.Msg));
                }
                else
                {
                    faultCount++;
                    stringList.Add(res.StateDetail);
                }
            }

            if (faultCount > 0)
            {
                Console.WriteLine("Fault message = " + faultCount);
                stringList.ForEach(Console.WriteLine);
            }

            return result;
        }

        private int GetNextClient()
        {
            var i = Interlocked.Increment(ref index);
            return (i % connection);
        }
    }
}
