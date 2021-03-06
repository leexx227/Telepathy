﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Session.QueueAdapter
{
    public static class CloudQueueConstants
    {
        public static string BrokerLauncherRequestQueueName => "brokerlaunchreq";

        public static string BrokerLauncherResponseQueueName => "brokerlaunchres";

        private const string BrokerWorkerControllerRequestQueueNamePrefix = "brokerworkerctrlreq";

        private const string BrokerWorkerControllerResponseQueueNamePrefix = "brokerworkerctrlres";

        public static string GetBrokerWorkerControllerRequestQueueName(string sessionId) => BrokerWorkerControllerRequestQueueNamePrefix + $"-{sessionId}";
        public static string GetBrokerWorkerControllerResponseQueueName(string sessionId) => BrokerWorkerControllerResponseQueueNamePrefix + $"-{sessionId}";
    }
}