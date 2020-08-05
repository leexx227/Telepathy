using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Telepathy.ProtoBuf;
using Google.Protobuf.WellKnownTypes;

namespace Microsoft.Telepathy.ClientAPI
{

    public class SessionClient
    {
        private string clientId;

        public string ClientID
        {
            get => clientId;
            set => clientId = value;
        }

        private Session session;

        private MethodDescriptor method;

        private ConcurrentQueue<Lazy<AsyncClientStreamingCall<InnerRequest, Empty>>> RequestCallQueue = new ConcurrentQueue<Lazy<AsyncClientStreamingCall<InnerRequest, Empty>>>();

        private List<Lazy<Frontend.FrontendClient>> clients = new List<Lazy<Frontend.FrontendClient>>();

        private ConcurrentQueue<InnerRequest> requestCache = new ConcurrentQueue<InnerRequest>();

        public SessionClient(string clientId, Session session, MethodDescriptor method, int connection = 10, int streamNum = 2)
        {
            this.clientId = clientId;
            this.method = method;
            this.session = session;

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
        { }

        public void SendRequest(IMessage request)
        {
            var inner = new InnerRequest
                { ServiceName = method.Service.FullName, MethodName = method.Name, Msg = request.ToByteString() };

            requestCache.Enqueue(inner);
            publish();
        }

        public async Task EndRequests()
        {
            throw new NotImplementedException();
            //Console.WriteLine("Begin end of request");
            //while (requestCache.Count > 0 || RequestCallQueue.Count < callNum)
            //{
            //    Console.WriteLine(requestCache.Count + " " + RequestCallQueue.Count);
            //    await Task.Delay(100);
            //}

            //Console.WriteLine("request cache is zero");
            //foreach (var callc in RequestCallQueue)
            //{
            //    await callc.Value.RequestStream.CompleteAsync();
            //    await callc.Value.ResponseAsync;
            //}

            //await clients[0].Value.EndRequestsAsync(new TotalNumber());
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
