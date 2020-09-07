using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Telepathy.ProtoBuf;
using StackExchange.Redis;

namespace Microsoft.Telepathy.Frontend.MessagePersist
{
    public class RedisPersist : IMessagePersist
    {
        private IDatabase redis;

        private string keyName;

        private int lastIndex = 0;

        private int totalNum = Int32.MaxValue;

        private RedisValue[] listCache = new RedisValue[0];

        private int cacheIndex = 0;

        private int count = 0;

        private ConcurrentDictionary<string, bool> messageDic = new ConcurrentDictionary<string, bool>();

        public RedisPersist(BatchClientIdentity batchInfo)
        {
            redis = CommonUtility.Connection.GetDatabase();
            keyName = getKeyName(batchInfo.SessionId, batchInfo.ClientId);

            Task.Run(async () => 
            {
                while (true)
                {
                    var temp = await redis.StringGetAsync(keyName + ":totalNum");
                    if (temp != RedisValue.Null && temp != RedisValue.EmptyString)
                    {
                        if (temp.TryParse(out totalNum))
                        {
                            break;
                        }
                    }
                    else
                    {
                        await Task.Delay(500);
                    }
                }
            });
        }

        public void Dispose()
        {
            //CommonUtility.Connection.Dispose();
        }

        public Task PutTaskAsync(InnerTask task)
        {
            throw new NotImplementedException();
        }

        public async Task<InnerResult> GetResultAsync()
        {
            while (count < totalNum)
            {
                if (listCache.Length == cacheIndex)
                {
                    listCache = await redis.ListRangeAsync(keyName, lastIndex, lastIndex + Configuration.MaxCacheTasks - 1);
                    lastIndex = lastIndex + listCache.Length;
                    cacheIndex = 0;
                }

                if (cacheIndex < listCache.Length)
                {
                    var result = InnerResult.Parser.ParseFrom(listCache[cacheIndex++]);
                    if (messageDic.TryAdd(result.MessageId, true))
                    {
                        count++;
                        return result;
                    }
                    else
                    {
                        continue;
                    }
                }

                await Task.Delay(100);
            }

            return null;
        }

        private static string getKeyName(string sessionId, string clientId)
        {
            var sb = new StringBuilder("{" + sessionId + "}");
            sb.Append(':');
            sb.Append(clientId);
            sb.Append(":response");
            return sb.ToString();
        }
}
}
