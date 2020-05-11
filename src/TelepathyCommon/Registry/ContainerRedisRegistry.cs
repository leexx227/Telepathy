// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using YamlDotNet.Serialization;

namespace Microsoft.Telepathy.Common.Registry
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using StackExchange.Redis;

    // Registry implementation using Redis
    public class ContainerRedisRegistry : IRegistry
    {
        private readonly IDatabase db;

        public ContainerRedisRegistry()
        {
            string redisAddress = Environment.GetEnvironmentVariable(TelepathyConstants.RedisHost) + ":" + Environment.GetEnvironmentVariable(TelepathyConstants.RedisPort);
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisAddress);
            db = redis.GetDatabase();
        }

        public static string SerializeObject(object o)
        {
            if (!o.GetType().IsSerializable)
            {
                return null;
            }

            using (MemoryStream stream = new MemoryStream())
            {
                new BinaryFormatter().Serialize(stream, o);
                return Convert.ToBase64String(stream.ToArray());
            }
        }

        public static object DeserializeObject(string str)
        {
            byte[] bytes = Convert.FromBase64String(str);

            using (MemoryStream stream = new MemoryStream(bytes))
            {
                return new BinaryFormatter().Deserialize(stream);
            }
        }

        public Task DeleteValueAsync(string key, string name, CancellationToken token)
        { 
            return db.KeyDeleteAsync(key);
        }

        public Task<IDictionary<string, string>> GetRegistryProperties(IList<string> propertyNames, CancellationToken token)
        {
            IDictionary<string, string> res = new Dictionary<string, string>();
            foreach (var name in propertyNames)
            {
                res.Add(name, db.StringGet(name));
            }

            return Task.FromResult(res);
        }

        public Task<T> GetValueAsync<T>(string key, string name, CancellationToken token, T defaultValue = default)
        {
            if (typeof(T) == typeof(string))
            {
                return Task.FromResult((T)(object)db.StringGetAsync(name).Result.ToString());
            }
            return Task.FromResult((T)DeserializeObject(db.StringGetAsync(name).Result));
        }

        public Task<object> GetValueAsync(string key, string name, CancellationToken token, object defaultValue = null)
        {
            return Task.FromResult(DeserializeObject(db.StringGet(name)));
        }

        public Task MonitorRegistryKeyAsync<T>(string key, string name, TimeSpan checkPeriod, EventHandler<RegistryValueChangedArgs<T>> callback, CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task SetRegistryProperties(IDictionary<string, object> properties, CancellationToken token)
        {
            foreach (var property in properties)
            {
              db.StringSetAsync(property.Key, SerializeObject(property.Value));
            }

            return Task.CompletedTask;
        }

        public Task SetValueAsync<T>(string key, string name, T value, CancellationToken token)
        {
            if(value.GetType().Name.Equals("String"))
            {
                return db.StringSetAsync(name, value.ToString());
            }
            else
            {
                return db.StringSetAsync(name, SerializeObject(value));
            }
        }
    }
}
