using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Telepathy.HostAgent.Common;

namespace Microsoft.Telepathy.HostAgent.Core
{
    public class EnvironmentInfo
    {
        private string svcHostName = "localhost";
        /// <summary>
        /// The host name of service. By default, the service host name is "localhost".
        /// </summary>
        public string SvcHostName
        {
            get { return this.svcHostName; }
        }

        private string dispatcherIp;
        /// <summary>
        /// The Ip address of dispatcher.
        /// </summary>
        public string DispatcherIp
        {
            get { return this.dispatcherIp; }
        }

        private int dispatcherPort = HostAgentConstants.DispatcherPort;
        /// <summary>
        /// The host port of dispatcher.
        /// </summary>
        public int DispatcherPort
        {
            get { return this.dispatcherPort; }
        }

        private int svcTimeoutMs = 3000;
        /// <summary>
        /// RPC timeout when calling service method. By default, the service timeout is 3000 ms.
        /// </summary>
        public int SvcTimeoutMs
        {
            get { return this.svcTimeoutMs; }
        }

        private int svcConcurrency = 1;
        /// <summary>
        /// Service concurrency config. By default, the service concurrency count is 1.
        /// </summary>
        public int SvcConcurrency
        {
            get { return this.svcConcurrency; }
        }

        private int prefetchCount = 1;
        /// <summary>
        /// Task prefetch config. By default, the prefetch count is 1.
        /// </summary>
        public int PrefetchCount
        {
            get { return this.prefetchCount; }
        }

        private string sessionId;
        /// <summary>
        /// The id of the current session.
        /// </summary>
        public string SessionId
        {
            get { return this.sessionId; }
        }

        private int svcInitTimeoutMs = 0;
        /// <summary>
        /// Timeout for service initialization and preparation. The default value is 0 ms.
        /// </summary>
        public int SvcInitTimeoutMs
        {
            get { return this.svcInitTimeoutMs; }
        }

        public EnvironmentInfo()
        {
            Utility.TryGetEnvironmentVariable<string>(HostAgentConstants.DispatcherIpEnvVar, ref this.dispatcherIp);
            Utility.TryGetEnvironmentVariable<int>(HostAgentConstants.SvcTimeoutEnvVar, ref this.svcTimeoutMs);
            Utility.TryGetEnvironmentVariable<int>(HostAgentConstants.SvcConcurrencyEnvVar, ref this.svcConcurrency);
            Utility.TryGetEnvironmentVariable<int>(HostAgentConstants.PrefetchCountEnvVar, ref this.prefetchCount);
            Utility.TryGetEnvironmentVariable<string>(HostAgentConstants.SessionIdEnvVar, ref this.sessionId);
            Utility.TryGetEnvironmentVariable<int>(HostAgentConstants.SvcInitTimeoutEnvVar, ref this.svcInitTimeoutMs);
        }
    }
}
