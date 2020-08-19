using Microsoft.Telepathy.HostAgent.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Telepathy.HostAgent.HostWrapper
{
    class HostAgentLoader
    {
        private List<string> mustNotNullVarList;

        private ProcessStartInfo processInfo;

        private const string cmd = HostAgentConstants.DotnetCommand;

        private const string program = @"Microsoft.Telepathy.HostAgent.Launcher.dll";

        public HostAgentLoader(List<string> mustNotNullVarList)
        {
            this.mustNotNullVarList = mustNotNullVarList;
            this.processInfo = new ProcessStartInfo(cmd, program);

            SetProcessStartInfo();
        }

        public Process LoadSvc(int svcPort)
        {
            this.processInfo.EnvironmentVariables[HostAgentConstants.SvcPortEnvVar] = svcPort.ToString();

            Console.WriteLine("Host agent info:");
            WrapperUtility.PrintProcessInfo(this.processInfo);
            try
            {
                return Process.Start(processInfo);
            }
            catch (Exception e)
            {
                Trace.TraceError($"Error occured when starting host agent process: {e.Message}");
                Console.WriteLine($"Error occured when starting host agent process: {e.Message}");
                throw;
            }
        }

        private void SetProcessStartInfo()
        {
            if (!WrapperUtility.CheckEnvVarValidation(this.mustNotNullVarList))
            {
                Console.WriteLine("Host agent process start info initialization failed. Get empty environment variable.");
                throw new InvalidOperationException("Host agent process start info initialization failed. Get empty environment variable.");
            }
            
            this.processInfo.WorkingDirectory = this.GetHostAgentWorkingDir;

            this.processInfo.EnvironmentVariables[HostAgentConstants.DispatcherIpEnvVar] = Environment.GetEnvironmentVariable(HostAgentConstants.DispatcherIpEnvVar);

            this.processInfo.EnvironmentVariables[HostAgentConstants.SvcTimeoutEnvVar] = Environment.GetEnvironmentVariable(HostAgentConstants.SvcTimeoutEnvVar);

            this.processInfo.EnvironmentVariables[HostAgentConstants.SvcConcurrencyEnvVar] = Environment.GetEnvironmentVariable(HostAgentConstants.SvcConcurrencyEnvVar);

            this.processInfo.EnvironmentVariables[HostAgentConstants.PrefetchCountEnvVar] = Environment.GetEnvironmentVariable(HostAgentConstants.PrefetchCountEnvVar);

            this.processInfo.EnvironmentVariables[HostAgentConstants.SessionIdEnvVar] = Environment.GetEnvironmentVariable(HostAgentConstants.SessionIdEnvVar);
        }

        private string GetHostAgentWorkingDir => string.Join(VariableSetter.GetFileSeparator(),
            new List<string>()
            {
                Environment.GetEnvironmentVariable(HostAgentConstants.TelepathyWorkingDirEnvVar),
                HostAgentConstants.HostAgentPath
            });
    }
}
