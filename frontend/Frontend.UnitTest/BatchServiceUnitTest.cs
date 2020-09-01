using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Core.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Telepathy.Frontend;
using Microsoft.Telepathy.Frontend.Services;
using Microsoft.Telepathy.ProtoBuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;
using NsqSharp;
using IMessage = NsqSharp.IMessage;

namespace Frontend.UnitTest
{
    [TestClass]
    public class BatchServiceUnitTest
    {
        public static string fakeMessage = "Fake message";

        private static string sessionId = "session";

        private static string clientId = "client";

        private static InnerTask fakeInnerTask = new InnerTask { SessionId = sessionId, ClientId = clientId, Msg = ByteString.CopyFromUtf8(fakeMessage) };

        private static string nsqLookupd;

        public static bool completed;

        public static string queueName = sessionId + "." + clientId;

        public static ServerCallContext testServerCallContext = TestServerCallContext.Create(null, null, DateTime.Now, null,
            new CancellationToken(), null, null, null, null, null, null);

        [TestInitialize]
        public void TestInit()
        {
            Configuration.NsqAddress = "";
            Configuration.RedisConnectString = "";
            Configuration.SessionServiceAddress = "";
            nsqLookupd = "";

            completed = false;
        }

        [TestMethod]
        public async Task SendTasksTest()
        {
            var service = new FrontendBatchService();

            var request = new TestAsyncStreamReader<InnerTask>(fakeInnerTask);

            var result = await service.SendTask(request, null);

            var consumer = new Consumer(queueName, "channel");
            consumer.AddHandler(new MessageHandler());
            consumer.ConnectToNsqLookupd(nsqLookupd);

            Assert.AreEqual(new Empty(), result);

            while (!completed)
            {
                await Task.Delay(100);
            }
        }

        [TestMethod]
        public async Task GetResultsTest()
        {
            var cache = CommonUtility.Connection.GetDatabase();

            await cache.KeyDeleteAsync(queueName);
            await cache.ListRightPushAsync(queueName, new InnerResult { Msg = ByteString.CopyFromUtf8(fakeMessage) }.ToByteArray());
            await cache.StringSetAsync(queueName + ".totalNum", 1);

            var service = new FrontendBatchService();

            var request = new BatchClientIdentity { SessionId = sessionId, ClientId = clientId };
            var result = new TestServerStreamWriter<InnerResult>();

            await service.GetResults(request, result, testServerCallContext);


            Assert.AreEqual(1, result.Responses.Count);
            result.Responses.ForEach(item => Assert.AreEqual(item.Msg.ToStringUtf8(), fakeMessage));
        }

        [TestMethod]
        public async Task GetLargeResultsTest()
        {
            var cache = CommonUtility.Connection.GetDatabase();

            int n = 1001;
            var batch = cache.CreateBatch();
            var tasks = new List<Task>();
            await cache.KeyDeleteAsync(queueName);
            for (int i = 0; i < n; i++)
            {
                var task = batch.ListRightPushAsync(queueName, new InnerResult { Msg = ByteString.CopyFromUtf8(fakeMessage)}.ToByteArray());
                tasks.Add(task);
            }

            batch.Execute();
            await Task.WhenAll(tasks.ToArray());

            await cache.StringSetAsync(queueName + ".totalNum", n);

            var service = new FrontendBatchService();

            var request = new BatchClientIdentity { SessionId = sessionId, ClientId = clientId };
            var result = new TestServerStreamWriter<InnerResult>();

            await service.GetResults(request, result, testServerCallContext);

            Assert.AreEqual(n, result.Responses.Count);
            result.Responses.ForEach(item => Assert.AreEqual(item.Msg.ToStringUtf8(), fakeMessage));
        }

        [TestMethod]
        public async Task EndTasksTest()
        {
            //TODO because endTasks func needs session service work
            //var cache = CommonUtility.Connection.GetDatabase();

            //await cache.KeyDeleteAsync(queueName + ".totalNum");

            //var service = new FrontendBatchService();
            //var request = new ClientEndOfTaskRequest { BatchClientInfo = new BatchClientIdentity { SessionId = sessionId, ClientId = clientId }, TotalRequestNumber = 5};

            //var result = await service.EndTasks(request, null);

            //var redisValue = cache.StringGet(queueName + ".totalNum");
            //redisValue.TryParse(out int number);

            //Assert.AreEqual(new Empty(), result);
            //Assert.AreEqual(5, number);
        }
    }

    public class MessageHandler : IHandler
    {
        public void HandleMessage(IMessage message)
        {
            InnerTask msg = InnerTask.Parser.ParseFrom(message.Body);

            Assert.AreEqual(msg.Msg.ToStringUtf8(), BatchServiceUnitTest.fakeMessage);

            Debug.WriteLine(msg.Msg.ToStringUtf8());

            BatchServiceUnitTest.completed = true;
        }

        public void LogFailedMessage(IMessage message)
        {
            throw new System.NotImplementedException();
        }
    }
}
