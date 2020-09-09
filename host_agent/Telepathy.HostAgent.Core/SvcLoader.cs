using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using Microsoft.Telepathy.HostAgent.Common;

namespace Microsoft.Telepathy.HostAgent.Core
{
    class SvcLoader
    {
        private ProcessStartInfo processInfo;

        private string cmd;

        private string program;

        public SvcLoader()
        {
            this.processInfo = new ProcessStartInfo();
            SetProcessStartInfo();
        }

        private void SetProcessStartInfo()
        {
            var svcFullPath = Environment.GetEnvironmentVariable(HostAgentConstants.SvcFullPathEnvVar);
            Utility.ThrowIfNullOrEmpty(svcFullPath, HostAgentConstants.SvcFullPathEnvVar);
            Utility.ThrowIfNullOrEmpty(Environment.GetEnvironmentVariable(HostAgentConstants.TelepathyWorkingDirEnvVar), HostAgentConstants.TelepathyWorkingDirEnvVar);
            svcFullPath = Environment.ExpandEnvironmentVariables(svcFullPath);

            var pathList = svcFullPath.Contains(HostAgentConstants.WinFilePathSeparator)
                ? svcFullPath.Split(HostAgentConstants.WinFilePathSeparator)
                : svcFullPath.Split(HostAgentConstants.UnixFilePathSeparator);
            this.program = pathList[pathList.Length-1];

            var workingDir = string.Join(Utility.GetFileSeparator(), pathList[0..(pathList.Length - 1)]);

            var language = Environment.GetEnvironmentVariable(HostAgentConstants.SvcLanguageEnvVar);
            Utility.ThrowIfNullOrEmpty(language, HostAgentConstants.SvcLanguageEnvVar);
            switch (language.ToLower())
            {
                case HostAgentConstants.CsharpLanguage:
                    this.cmd = HostAgentConstants.DotnetCommand;
                    break;
                case HostAgentConstants.JavaLanguage:
                    this.cmd = HostAgentConstants.JavaCommand;
                    this.program = HostAgentConstants.JarPrefix + this.program;
                    break;
                case HostAgentConstants.PythonLanguage:
                    this.cmd = HostAgentConstants.PythonCommand;
                    break;
                default:
                    Console.WriteLine($"Only support csharp, java and python. Current language config: {language}.");
                    Trace.TraceError($"Only support csharp, java and python. Current language config: {language}.");
                    throw new InvalidOperationException($"Only support csharp, java and python.");
            }

            this.processInfo.FileName = this.cmd;
            this.processInfo.Arguments = this.program;
            this.processInfo.WorkingDirectory = workingDir;
        }

        public Process LoadSvc(int svcPort)
        {
            if (svcPort < 0)
            {
                throw new InvalidOperationException($"Service port invalid: {svcPort}");
            }
            this.processInfo.EnvironmentVariables[HostAgentConstants.SvcPortEnvVar] = svcPort.ToString();
            try
            {
                Console.WriteLine("Service info:");
                Utility.PrintProcessInfo(this.processInfo);
                var process = Process.Start(processInfo);
                return process;
            }
            catch (Exception e)
            {
                Trace.TraceError($"Error occured when starting service process: {e.Message}");
                Console.WriteLine($"Error occured when starting service process: {e.Message}");
                throw;
            }
        }
    }
}

