using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Telepathy.ProtoBuf;
using NsqSharp;

namespace Microsoft.Telepathy.Frontend.MessagePersist
{
    public class NsqPersist : IMessagePersist
    {
        private string topicName = null;

        private Producer producer;

        private List<byte[]> taskList = new List<byte[]>();

        public NsqPersist()
        {
            producer = new Producer(Configuration.NsqAddress, new Config {DialTimeout = TimeSpan.FromSeconds(10)});
        }

        public void Dispose()
        {
            if (taskList.Count > 0)
            {
                producer.MultiPublishAsync(topicName, taskList).GetAwaiter().GetResult();
            }
        }

        public async Task PutTaskAsync(InnerTask task)
        {
            try
            {
                //await producer.PublishAsync(GetTopicName(sessionId, clientId), request.ToByteArray());
                taskList.Add(task.ToByteArray());
                if (string.IsNullOrEmpty(topicName))
                {
                    topicName = GetTopicName(task.SessionId, task.ClientId);
                }

                if (taskList.Count >= Configuration.MaxCacheTasks)
                {
                    await producer.MultiPublishAsync(topicName, taskList);

                    taskList.Clear();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public Task<InnerResult> GetResultAsync()
        {
            throw new NotImplementedException();
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
