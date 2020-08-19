using System;
using Microsoft.Telepathy.HostAgent.Common;
using Microsoft.Telepathy.HostAgent.Core;
using Microsoft.Telepathy.HostAgent.Interface;

namespace Microsoft.Telepathy.HostAgent.Launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            //SetEnvironmentVariable();
            var environmentInfo = new EnvironmentInfo();

            IHostAgent hostAgent = new Microsoft.Telepathy.HostAgent.Core.HostAgent(environmentInfo);

            hostAgent.StartAsync().GetAwaiter().GetResult();

            Console.ReadKey();
        }

        static void SetEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcPortEnvVar, "5001");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcConcurrencyEnvVar, "4");
            Environment.SetEnvironmentVariable(HostAgentConstants.PrefetchCountEnvVar, "10");
            Environment.SetEnvironmentVariable(HostAgentConstants.DispatcherIpEnvVar, "localhost");
            Environment.SetEnvironmentVariable(HostAgentConstants.DispatcherPortEnvVar, "5000");
            Environment.SetEnvironmentVariable(HostAgentConstants.SessionIdEnvVar, "123456");
        }
    }
}
