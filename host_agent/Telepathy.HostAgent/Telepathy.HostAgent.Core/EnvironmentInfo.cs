using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Telepathy.HostAgent.Core
{
    public class EnvironmentInfo
    {
        public string SvcHostName { get; }
        public int SvcPort { get; }
        public string DispatcherIp { get; }
        public int DispatcherPort { get; }
        public TimeSpan SvcTimeout { get; }
        public int SvcConcurrency { get; }
        public int PrefetchCount { get; set; } = 1;
    }
}
