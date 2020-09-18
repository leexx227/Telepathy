// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Session
{
    using System;
    using System.Collections.Generic;

    public class SessionInitInfo
    {
        public string ServiceName { get; }

        public Version ServiceVersion { get; set; }

        public bool Durable { get; }

        public int MaxServiceInstance { get; }

        public int SessionIdleTimeout { get; }

        public int ClientIdleTimeout { get;}

        public string SessionCreator { get; }

        public int? ServiceTimeout { get; }

        public int? PrefetchCount { get; }

        public Dictionary<string, string> Environments;

        private int defaultIdleTimeout = 3600000;

        public SessionInitInfo(string serviceName, Version serviceVersion, bool durable,
            int maxServiceInstance, string sessionCreator, int sessionIdleTimeout = 3600000, int clientIdleTimeout = 3600000)
        {
            ServiceName = serviceName;
            ServiceVersion = serviceVersion;
            Durable = durable;
            MaxServiceInstance = maxServiceInstance;
            SessionIdleTimeout = sessionIdleTimeout;
            ClientIdleTimeout = clientIdleTimeout;
            SessionCreator = sessionCreator;
        }

        public SessionInitInfo(string serviceName, Version serviceVersion, bool durable,
            int maxServiceInstance, string sessionCreator, int serviceTimeout, int sessionIdleTimeout = 3600000, int clientIdleTimeout = 3600000)
        {
            ServiceName = serviceName;
            ServiceVersion = serviceVersion;
            Durable = durable;
            MaxServiceInstance = maxServiceInstance;
            SessionIdleTimeout = sessionIdleTimeout;
            ClientIdleTimeout = clientIdleTimeout;
            SessionCreator = sessionCreator;
            ServiceTimeout = serviceTimeout;
        }

        public SessionInitInfo(string serviceName, Version serviceVersion, bool durable,
            int maxServiceInstance, int sessionIdleTimeout, int clientIdleTimeout, string sessionCreator, int serviceTimeout, int prefetchCount)
        {
            ServiceName = serviceName;
            ServiceVersion = serviceVersion;
            Durable = durable;
            MaxServiceInstance = maxServiceInstance;
            SessionIdleTimeout = sessionIdleTimeout;
            ClientIdleTimeout = clientIdleTimeout;
            SessionCreator = sessionCreator;
            ServiceTimeout = serviceTimeout;
            PrefetchCount = prefetchCount;
        }

        public SessionInitInfo(string serviceName, Version serviceVersion, bool durable,
            int maxServiceInstance, int sessionIdleTimeout, int clientIdleTimeout, string sessionCreator, int serviceTimeout, int prefetchCount, Dictionary<string, string> environments)
        {
            ServiceName = serviceName;
            ServiceVersion = serviceVersion;
            Durable = durable;
            MaxServiceInstance = maxServiceInstance;
            SessionIdleTimeout = sessionIdleTimeout;
            ClientIdleTimeout = clientIdleTimeout;
            SessionCreator = sessionCreator;
            ServiceTimeout = serviceTimeout;
            PrefetchCount = prefetchCount;
            Environments = environments;
        }
    }
}
