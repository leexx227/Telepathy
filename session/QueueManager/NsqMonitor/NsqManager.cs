// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System.Threading.Tasks;

namespace Microsoft.Telepathy.QueueManager.NsqMonitor
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using NsqSharp.Api;

    public class NsqManager
    {
        private const int WaitTime = 3000;
        private static readonly TimeSpan RequestTimeout = new TimeSpan(0, 0, 0, 0, 5000);
        private static Lazy<List<NsqLookupdHttpClient>> _lookupdHttpClients = new Lazy<List<NsqLookupdHttpClient>>(() => ConstructLookupdClients());

        private static List<string> GetLookupdAddress()
        {
            return new List<string> { "172.16.0.10:4161" };
        }

        private static List<NsqLookupdHttpClient> LookupdHttpClients { get => _lookupdHttpClients.Value; }
        private static List<NsqLookupdHttpClient> ConstructLookupdClients()
        {
            List<NsqLookupdHttpClient> lookupdHttpClients = new List<NsqLookupdHttpClient>();
            List<string> LookupdAddress = GetLookupdAddress();
            Console.WriteLine($"[NsqManager] Start to construct lookupd client.");
            foreach (string lookupdAddress in LookupdAddress)
            {
                lookupdHttpClients.Add(new NsqLookupdHttpClient(lookupdAddress, RequestTimeout));
            }
            Console.WriteLine($"[NsqManager] Current lookupd client number is {lookupdHttpClients.Count}");
            return lookupdHttpClients;
        }

        private static List<string> GetAllNsqdAddress(string topicName, int timeout)
        {
            Console.WriteLine($"[NsqManager] Start to get all Nsqd addresses for topic {topicName}.");
            List<string> nsqdAddress = new List<string>();
            DateTime startClientTime = DateTime.Now;
            while (true)
            {
                DateTime queryClientTime = DateTime.Now;
                if (queryClientTime - startClientTime >= TimeSpan.FromMilliseconds(timeout))
                {
                    break;
                }

                foreach (var lookupdHttpClient in LookupdHttpClients)
                {
                    try
                    {
                        NsqLookupdLookupResponse response = lookupdHttpClient.Lookup(topicName);

                        TopicProducerInformation[] producers = response.Producers;
                        foreach (var producer in producers)
                        {
                            string address = $"{producer.BroadcastAddress}/{producer.HttpPort}";
                            nsqdAddress.Add(address);
                        }

                        break;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"[NsqManager] Exception occurs when look up {topicName} response: {e.Message}");
                        Task.Delay(WaitTime);
                    }
                }
            }

          
            Console.WriteLine($"[NsqManager] Current nsqd client number is {nsqdAddress.Count}");
            return nsqdAddress;
        }

        public static List<NsqdHttpClient> GetAllNsqdClients(string topicName, int timeout)
        {
            List<string> nsqdAddress = GetAllNsqdAddress(topicName, timeout);
            List<NsqdHttpClient> nsqdHttpClients = new List<NsqdHttpClient>();
            if (nsqdAddress.Count == 0)
            {
                Console.WriteLine($"[NsqManager] Fail to get nsqd addresses and the client is timeout to wait.");
                return nsqdHttpClients;
            }

            Console.WriteLine($"[NsqManager] Start to get all Nsqd clients.");
          
            foreach (var address in nsqdAddress)
            {
                nsqdHttpClients.Add(new NsqdHttpClient(address, RequestTimeout));
            }
            Console.WriteLine($"[NsqManager] End to construct all Nsqd clients.");
            return nsqdHttpClients;
        }

        private static int GetAllQueuesDepth(List<NsqdHttpClient> clients, string topicName)
        {
            Console.WriteLine($"[NsqManager] Start to get requests number for {topicName}.");
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
            Console.WriteLine($"[NsqManager] Current requests number for {topicName} is {queueDepth}.");
            return queueDepth;
        }

        public static int GetRequestNumber(List<NsqdHttpClient> allNsqdHttpClients, string batchQueueId)
        {
            return GetAllQueuesDepth(allNsqdHttpClients, batchQueueId);
        }
    }
}
