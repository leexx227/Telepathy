using System;
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

        public RedisPersist(BatchClientIdentity batchInfo)
        {
            redis = CommonUtility.Connection.GetDatabase();
            keyName = getKeyName(batchInfo.SessionId, batchInfo.ClientId);

            Task.Run(async () =>
            {
                while (!(await redis.StringGetAsync(keyName + ".totalNum")).TryParse(out totalNum)) {
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
            while (lastIndex < totalNum || cacheIndex < listCache.Length)
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
                    return result;
                }

                await Task.Delay(100);
            }

            return null;
        }

        private static string getKeyName(string sessionId, string clientId)
        {
            var sb = new StringBuilder(sessionId);
            sb.Append('.');
            sb.Append(clientId);
            return sb.ToString();
        }
}
}
