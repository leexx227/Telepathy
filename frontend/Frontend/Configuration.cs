using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StackExchange.Redis;

namespace Microsoft.Telepathy.Frontend
{
    public class Configuration
    {
        public static string SessionSvcAddrName = "SessionServiceAddress";

        public static string NsqAddrName = "NsqAddress";

        public static string RedisConnectStringName = "RedisConnectString";
        
        public static string SessionServiceAddress;

        public static string NsqAddress;

        public static int MaxCacheTasks = 1000;

        public static string RedisConnectString;
    }

    public class CommonUtility
    {
        private static Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() =>
        {
            return ConnectionMultiplexer.Connect(Configuration.RedisConnectString);
        });

        public static ConnectionMultiplexer Connection
        {
            get
            {
                return lazyConnection.Value;
            }
        }
    }
}
