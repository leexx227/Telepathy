using System;
using Microsoft.Telepathy.HostAgent.Common;
using Microsoft.Telepathy.HostAgent.Core;
using Microsoft.Telepathy.HostAgent.Interface;

namespace Telepathy.HostAgent.Launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            SetEnvironmentVariable();
            var parser = new EnvironmentParser();
            var environmentInfo = new EnvironmentInfo(parser);

            IHostAgent hostAgent = new Microsoft.Telepathy.HostAgent.Core.HostAgent(environmentInfo);

            hostAgent.StartAsync().GetAwaiter().GetResult();

            Console.ReadKey();
        }

        static void SetEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcPortVariable, "5001");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcConcurrencyVariable, "4");
            Environment.SetEnvironmentVariable(HostAgentConstants.PrefetchCountVariable, "10");
            Environment.SetEnvironmentVariable(HostAgentConstants.DispatcherIpVariable, "localhost");
            Environment.SetEnvironmentVariable(HostAgentConstants.DispatcherPortVariable, "5000");
        }
    }
}
