using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using Microsoft.Telepathy.HostAgent.Common;

namespace Microsoft.Telepathy.HostAgent.Core
{
    class SvcLoader
    {
        private List<string> mustNotNullVarList;

        private ProcessStartInfo processInfo;

        private string cmd;

        private string program;

        private int portStart = 5001;

        public SvcLoader(List<string> mustNotNullVarList)
        {
            this.mustNotNullVarList = mustNotNullVarList;
            this.processInfo = new ProcessStartInfo();
            SetProcessStartInfo();
        }
        public static List<string> GetSvcMustVariableList()
        {
            return new List<string>()
            {
                HostAgentConstants.SvcLanguageEnvVar, HostAgentConstants.TelepathyWorkingDirEnvVar,
                HostAgentConstants.SvcFullPathEnvVar
            };
        }

        private void SetProcessStartInfo()
        {
            if (!Utility.CheckEnvVarValidation(this.mustNotNullVarList))
            {
                throw new InvalidOperationException("Service process start info initialization failed. Get empty environment variable.");
            }

            var svcFullPath = Environment.GetEnvironmentVariable(HostAgentConstants.SvcFullPathEnvVar);
            svcFullPath = Environment.ExpandEnvironmentVariables(svcFullPath);
            var pathList = svcFullPath.Contains(HostAgentConstants.WinFilePathSeparator)
                ? svcFullPath.Split(HostAgentConstants.WinFilePathSeparator)
                : svcFullPath.Split(HostAgentConstants.UnixFilePathSeparator);
            this.program = pathList[pathList.Length-1];

            var workingDir = string.Join(Utility.GetFileSeparator(), pathList[0..(pathList.Length - 1)]);

            var language = Environment.GetEnvironmentVariable(HostAgentConstants.SvcLanguageEnvVar);
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
                var process = Process.Start(processInfo);
                Console.WriteLine("Service info:");
                Utility.PrintProcessInfo(this.processInfo);
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

