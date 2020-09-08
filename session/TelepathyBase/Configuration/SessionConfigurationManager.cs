// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.



namespace Microsoft.Telepathy.Common
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Newtonsoft.Json;

    public static class SessionConfigurationManager
    {
        public static string GetBatchClientQueueId(string sessionId, string batchId)
        {
            return $"{{{sessionId}}}:{batchId}";
        }

        public static string GetRedisBatchClientStateKey(string sessionId)
        {
            return $"{{{sessionId}}}:batchState";
        }

        public static string GetRedisBatchClientIdKey(string sessionId)
        {
            return $"{{{sessionId}}}:batchIds";
        }

        public static string GetRedisSessionStateKey(string sessionId)
        {
            return $"{{{sessionId}}}:state";
        }

        private static string _redisConnectionKey;

        public static string GetRedisConnectionString()
        {
            return _redisConnectionKey;
        }

        public static AzureBatchSessionConfiguration ConfigureAzureBatchSessionFromJsonFile(string filePath)
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                var serviceConfigurations = reader.ReadToEnd();
                AzureBatchSessionConfiguration currentConfiguration = JsonConvert.DeserializeObject<AzureBatchSessionConfiguration>(serviceConfigurations);
                _redisConnectionKey = currentConfiguration.RedisConnectionKey;
                return currentConfiguration;
            }
        }
    }
}
