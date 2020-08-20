using System;
using System.Threading;
using Microsoft.Telepathy.HostAgent.Common;
using Microsoft.Telepathy.HostAgent.Core;
using Microsoft.Telepathy.HostAgent.Interface;

namespace Microsoft.Telepathy.HostAgent.Launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            SetEnvironmentVariable();
            var environmentInfo = new EnvironmentInfo();

            IHostAgent hostAgent = new Microsoft.Telepathy.HostAgent.Core.HostAgent(environmentInfo);

            hostAgent.StartAsync().GetAwaiter().GetResult();

            Console.ReadKey();
        }

        static void SetEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(HostAgentConstants.DispatcherIpEnvVar, "localhost");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcLanguageEnvVar, "csharp");
            Environment.SetEnvironmentVariable(HostAgentConstants.SessionIdEnvVar, "123456");
            Environment.SetEnvironmentVariable(HostAgentConstants.TelepathyWorkingDirEnvVar, @".\");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcFullPathEnvVar, @"%TELEPATHY_WORKING_DIR%testsvc\GreeterServer.dll");

            Environment.SetEnvironmentVariable(HostAgentConstants.SvcConcurrencyEnvVar, "3");
            Environment.SetEnvironmentVariable(HostAgentConstants.PrefetchCountEnvVar, "10");
        }
    }
}
