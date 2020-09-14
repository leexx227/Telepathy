// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace Microsoft.Telepathy.HostAgent.Launcher
{
    using System;

    using Microsoft.Telepathy.HostAgent.Common;
    using Microsoft.Telepathy.HostAgent.Core;
    using Microsoft.Telepathy.HostAgent.Interface;

    class Program
    {
        static void Main(string[] args)
        {
            //SetEnvironmentVariable();
            var environmentInfo = new EnvironmentInfo();

            var hostAgent = new HostAgent(environmentInfo);

            hostAgent.StartAsync().GetAwaiter().GetResult();

            Console.ReadKey();

            hostAgent.Dispose();
        }

        static void SetEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(HostAgentConstants.DispatcherIpEnvVar, "localhost");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcLanguageEnvVar, "csharp");
            Environment.SetEnvironmentVariable(HostAgentConstants.SessionIdEnvVar, "agent-0");
            Environment.SetEnvironmentVariable(HostAgentConstants.TelepathyWorkingDirEnvVar, @".\");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcFullPathEnvVar, @"%TELEPATHY_WORKING_DIR%testsvc\csharp\EchoServer.dll");

            Environment.SetEnvironmentVariable(HostAgentConstants.SvcConcurrencyEnvVar, "1");
            Environment.SetEnvironmentVariable(HostAgentConstants.PrefetchCountEnvVar, "3");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcTimeoutEnvVar, "500");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcInitTimeoutEnvVar, "10000");
        }
    }
}
