// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace Microsoft.Telepathy.HostAgent.UnitTest
{
    using Microsoft.Telepathy.HostAgent.Common;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Google.Protobuf;
    using Google.Protobuf.Reflection;
    using Google.Protobuf.WellKnownTypes;
    using Grpc.Core;

    using Microsoft.Telepathy.HostAgent.Core;
    using Microsoft.Telepathy.ProtoBuf;
    using Microsoft.Telepathy.HostAgent.Interface;

    [TestClass]
    public class HostAgentTest
    {
        private static int dispatcherPort = 50051;

        private static int svcPort = 5000;

        private static string svcHostName = "localhost";

        private static Server dispatcher;

        private static Server service;

        private static string message = "hello";

        private static EchoRequest echoRequest = new EchoRequest() { Message = message, DelayTime = 0 };

        private static MethodDescriptor md = Echo.Descriptor.FindMethodByName("Echo");

        private static InnerTask innerTask = new InnerTask() { ServiceName = md.Service.FullName, MethodName = md.Name, MethodType = MethodEnum.Unary, Msg = echoRequest.ToByteString() };

        private static WrappedTask wrappedTask = new WrappedTask { SerializedInnerTask = innerTask.ToByteString(), SessionState = SessionStateEnum.Running };

        private static HostAgent hostAgent;

        [TestInitialize]
        public void Initialize()
        {
            Environment.SetEnvironmentVariable(HostAgentConstants.DispatcherIpEnvVar, "localhost");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcLanguageEnvVar, "csharp");
            Environment.SetEnvironmentVariable(HostAgentConstants.SessionIdEnvVar, "agent-0");
            Environment.SetEnvironmentVariable("TruePath", @".\");
            Environment.SetEnvironmentVariable(HostAgentConstants.TelepathyWorkingDirEnvVar, @"TruePath");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcPathEnvVar, @"testsvc\csharp\EchoServer.dll");

            Environment.SetEnvironmentVariable(HostAgentConstants.SvcConcurrencyEnvVar, "1");
            Environment.SetEnvironmentVariable(HostAgentConstants.PrefetchCountEnvVar, "3");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcTimeoutEnvVar, "500");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcInitTimeoutEnvVar, "0");

            hostAgent = new HostAgent(new EnvironmentInfo());
            hostAgent.maxRetries = 1;
        }

        [TestMethod]
        public async Task GetTaskAsyncTest1()
        {
            dispatcher = FakeDispatcher.GetDispatcher(dispatcherPort, new FakeDispatcher.Normal());
            dispatcher.Start();

            var task = hostAgent.GetTaskAsync();
            await Task.Delay(2000);
            hostAgent.Stop();

            Assert.AreEqual(hostAgent.prefetchCount, hostAgent.taskQueue.Count);
            hostAgent.taskQueue.Clear();
            dispatcher.ShutdownAsync().Wait();
        }

        [TestMethod]
        public async Task GetTaskAsyncTest2()
        {
            dispatcher = FakeDispatcher.GetDispatcher(dispatcherPort, new FakeDispatcher.TempNoTask());
            dispatcher.Start();

            var task = hostAgent.GetTaskAsync();
            await Task.Delay(2000);
            hostAgent.Stop();

            Assert.AreEqual(0, hostAgent.taskQueue.Count);
            dispatcher.ShutdownAsync().Wait();
        }

        [TestMethod]
        public async Task GetTaskAsyncTest3()
        {
            dispatcher = FakeDispatcher.GetDispatcher(dispatcherPort, new FakeDispatcher.EndTask());
            dispatcher.Start();

            await hostAgent.GetTaskAsync();

            Assert.AreEqual(0, hostAgent.taskQueue.Count);
            dispatcher.ShutdownAsync().Wait();
        }

        [TestMethod]
        public async Task SendResultAsyncTest1()
        {
            dispatcher = FakeDispatcher.GetDispatcher(dispatcherPort, new FakeDispatcher.Normal());
            dispatcher.Start();

            var result = new SendResultRequest();
            var reply = await hostAgent.SendResultAsync(result);

            Assert.AreEqual(new Empty(), reply);
            dispatcher.ShutdownAsync().Wait();
        }

        [TestMethod]
        public async Task HandleUnaryCallTest1()
        {
            service = FakeService.GetService(svcPort, new FakeService.CorrectService());
            service.Start();

            hostAgent.svcChannel = new Channel(svcHostName, svcPort, ChannelCredentials.Insecure);
            var callInvoker = hostAgent.svcChannel.CreateCallInvoker();
            var resultMessage = await hostAgent.HandleUnaryCall(callInvoker, innerTask);
            var echoReply = EchoReply.Parser.ParseFrom(resultMessage.Msg);

            Assert.AreEqual(message, echoReply.Message);
            service.ShutdownAsync().Wait();
        }

        [TestMethod]
        public async Task CallMethodWrapperAsyncTest1()
        {
            service = FakeService.GetService(svcPort, new FakeService.CorrectService());
            service.Start();

            hostAgent.svcChannel = new Channel(svcHostName, svcPort, ChannelCredentials.Insecure);
            var result = await hostAgent.CallMethodWrapperAsync(wrappedTask);
            var innerResult = InnerResult.Parser.ParseFrom(result.SerializedInnerResult);
            var echoReply = EchoReply.Parser.ParseFrom(innerResult.Msg);

            Assert.AreEqual(message, echoReply.Message);
            Assert.IsTrue(result.TaskState == TaskStateEnum.Finished);
            service.ShutdownAsync().Wait();
        }

        [TestMethod]
        public async Task CallMethodWrapperAsyncTest2()
        {
            service = FakeService.GetService(svcPort, new FakeService.ExceptionService());
            service.Start();

            hostAgent.svcChannel = new Channel(svcHostName, svcPort, ChannelCredentials.Insecure);
            var result = await hostAgent.CallMethodWrapperAsync(wrappedTask);

            Assert.IsTrue(result.TaskState == TaskStateEnum.Finished);
            service.ShutdownAsync().Wait();
        }

        [TestMethod]
        public async Task CallMethodWrapperAsyncTest3()
        {
            hostAgent.svcChannel = new Channel(svcHostName, svcPort, ChannelCredentials.Insecure);
            var result = await hostAgent.CallMethodWrapperAsync(wrappedTask);

            Assert.IsTrue(result.TaskState == TaskStateEnum.Requeue);
        }

        [TestMethod]
        public async Task IsSvcInitTimeoutTest1()
        {
            hostAgent.svcInitTimeoutMs = 2000;
            hostAgent.svcInitSw.Start();
            await Task.Delay(1000);

            Assert.IsFalse(hostAgent.IsSvcInitTimeout());
            hostAgent.svcInitSw.Stop();
            hostAgent.svcInitTimeoutMs = 0;
        }

        [TestMethod]
        public async Task IsSvcInitTimeoutTest2()
        {
            hostAgent.svcInitTimeoutMs = 500;
            hostAgent.svcInitSw.Start();
            await Task.Delay(1000);

            Assert.IsTrue(hostAgent.svcInitSw.IsRunning);
            Assert.IsTrue(hostAgent.IsSvcInitTimeout());
            Assert.IsFalse(hostAgent.svcInitSw.IsRunning);
            hostAgent.svcInitTimeoutMs = 0;
        }

        [TestMethod]
        public async Task IsSvcInitTimeoutTest3()
        {
            hostAgent.svcInitTimeoutMs = 500;
            hostAgent.svcInitSw.Start();
            await Task.Delay(1000);
            hostAgent.svcInitSw.Stop();

            Assert.IsFalse(hostAgent.svcInitSw.IsRunning);
            Assert.IsTrue(hostAgent.IsSvcInitTimeout());
            hostAgent.svcInitTimeoutMs = 0;
        }
    }
}
