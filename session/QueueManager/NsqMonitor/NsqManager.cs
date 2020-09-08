// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.QueueManager.NsqMonitor
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using NsqSharp.Api;

    public class NsqManager
    {
        private static TimeSpan RequestTimeout = new TimeSpan(5000);
        private static List<NsqLookupdHttpClient> _lookupdHttpClients = new List<NsqLookupdHttpClient>();

        private static List<string> GetLookupdAddress()
        {
            return new List<string> { "10.94.201.221:4161" };
        }

        private static void ConstructLookupdClients()
        {
            List<string> LookupdAddress = GetLookupdAddress();
            foreach (string lookupdAddress in LookupdAddress)
            {
                _lookupdHttpClients.Add(new NsqLookupdHttpClient(lookupdAddress, RequestTimeout));
            }
        }

        private static List<string> GetAllNsqdAddress(string topicName)
        {
            ConstructLookupdClients();
            List<string> nsqdAddress = new List<string>();
            foreach (var lookupdHttpClient in _lookupdHttpClients)
            {
                NsqLookupdLookupResponse response = lookupdHttpClient.Lookup(topicName);
                TopicProducerInformation[] producers = response.Producers;
                foreach (var producer in producers)
                {
                    string address = $"{producer.BroadcastAddress}/{producer.HttpPort}";
                    nsqdAddress.Add(address);
                }
            }
            return nsqdAddress;
        }

        public static List<NsqdHttpClient> GetAllNsqdClients(string topicName)
        {
            List<string> nsqdAddress = GetAllNsqdAddress(topicName);
            List<NsqdHttpClient> nsqdHttpClients = new List<NsqdHttpClient>();
            foreach (var address in nsqdAddress)
            {
                nsqdHttpClients.Add(new NsqdHttpClient(address, RequestTimeout));
            }
            return nsqdHttpClients;
        }

        private static int GetAllQueuesDepth(List<NsqdHttpClient> clients, string topicName)
        {
            int queueDepth = 0;
            foreach (var client in clients)
            {
                NsqdStats stats = client.GetStats();
                NsqdStatsTopic[] topics = stats.Topics;
                NsqdStatsTopic targetTopic = Array.Find(topics, (topic) =>  topic.TopicName == topicName);
                if (targetTopic == null)
                    continue;
                queueDepth += targetTopic.Depth;
            }

            return queueDepth;
        }

        public static int GetRequestNumber(List<NsqdHttpClient> allNsqdHttpClients, string batchQueueId)
        {
            return GetAllQueuesDepth(allNsqdHttpClients, batchQueueId);
        }
    }
}
