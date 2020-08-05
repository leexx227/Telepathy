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

    public class SessionClient : IDisposable
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

        private ConcurrentQueue<AsyncClientStreamingCall<InnerRequest, Empty>> RequestCallQueue = new ConcurrentQueue<AsyncClientStreamingCall<InnerRequest, Empty>>();

        private List<Lazy<Frontend.FrontendClient>> clients = new List<Lazy<Frontend.FrontendClient>>();

        private ConcurrentQueue<InnerRequest> requestCache = new ConcurrentQueue<InnerRequest>();

        private int requestCallLock = 0;

        private int connection;

        private int streamNum;

        private int totalCall = 0;

        private int requestCount = 0;

        public SessionClient(string clientId, Session session, MethodDescriptor method, int connection = 10, int streamNum = 2)
        {
            this.clientId = clientId;
            this.method = method;
            this.session = session;
            this.connection = connection;
            this.streamNum = streamNum;

            if (method.IsClientStreaming)
            {
                if (method.IsServerStreaming)
                    methodtype = MethodEnum.DuplexStream;
                else
                    methodtype = MethodEnum.ClientStream;
            }
            else
            {
                if (method.IsServerStreaming)
                    methodtype = MethodEnum.ServerStream;
                else
                    methodtype = MethodEnum.Unary;
            }


            for (int i = 0; i < connection; i++)
            {
                var channel = GrpcChannel.ForAddress(session.TelepathyAddress);
                var client = new Frontend.FrontendClient(channel);
                clients.Add(new Lazy<Frontend.FrontendClient>(() => new Frontend.FrontendClient(channel)));
            }

            session.CreateSessionClient().GetAwaiter().GetResult();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        private void publish()
        {
            var temp = Interlocked.Exchange(ref requestCallLock, 1);
            if (temp == 0)
            {
                for (int i = 0; i < connection; i++)
                {
                    for (int j = 0; j < streamNum; j++)
                    {
                        RequestCallQueue.Enqueue(clients[i].Value.SendRequest());
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

        public void SendRequest(IMessage request)
        {
            this.SendRequest(request, Guid.NewGuid().ToString());
        }

        public void SendRequest(IMessage request, string messageId)
        {
            if (request.GetType() == method.InputType.ClrType)
            {
                var inner = new InnerRequest
                    { ServiceName = method.Service.FullName, MethodName = method.Name, Msg = request.ToByteString(), MethodType = methodtype, MessageId = messageId, ClientId = clientId, SessionId = session.SessionInfo.Id};

                requestCache.Enqueue(inner);
                Interlocked.Increment(ref requestCount);
                publish();
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public async Task EndRequests()
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

            await clients[0].Value.EndRequestsAsync(new TotalNumber());
        }

        public async Task<IEnumerable<TResponse>> GetResponses<TResponse>() where TResponse : IMessage<TResponse>, new()
        {
            var call = clients[0].Value.GetResponses(new Empty());
            var result = new List<TResponse>();

            await foreach (var res in call.ResponseStream.ReadAllAsync())
            {
                var v = new MessageParser<TResponse>(() => new TResponse());
                result.Add(v.ParseFrom(res.Msg));
            }

            return result;
        }
    }
}
