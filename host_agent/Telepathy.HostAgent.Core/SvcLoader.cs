using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Telepathy.HostAgent.Common;

namespace Microsoft.Telepathy.HostAgent.Core
{
    class SvcLoader
    {
        private ProcessStartInfo processInfo;

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
            this.processInfo.WorkingDirectory = workingDir;

            var language = Environment.GetEnvironmentVariable(HostAgentConstants.SvcLanguageEnvVar);
            Utility.ThrowIfNullOrEmpty(language, HostAgentConstants.SvcLanguageEnvVar);
            switch (language.ToLower())
            {
                case HostAgentConstants.CsharpLanguage:
                    this.SetCsharpProgramInfo(svcFullPath);
                    break;
                case HostAgentConstants.JavaLanguage:
                    this.SetJavaProgramInfo();
                    break;
                case HostAgentConstants.PythonLanguage:
                    this.SetPythonProgramInfo(svcFullPath);
                    break;
                default:
                    Console.WriteLine($"Only support csharp, java and python. Current language config: {language}.");
                    Trace.TraceError($"Only support csharp, java and python. Current language config: {language}.");
                    throw new InvalidOperationException($"Only support csharp, java and python.");
            }
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

        private void SetCsharpProgramInfo(string svcFullPath)
        {
            if (this.program.EndsWith(".exe"))
            {
                this.processInfo.FileName = svcFullPath;
                return;
            }

            if (this.program.EndsWith(".dll"))
            {
                this.processInfo.FileName = HostAgentConstants.DotnetCommand;
                this.processInfo.Arguments = this.program;
                return;
            }

            this.processInfo.FileName = svcFullPath;
        }

        private void SetJavaProgramInfo()
        {
            this.processInfo.FileName = HostAgentConstants.JavaCommand;
            this.processInfo.Arguments = HostAgentConstants.JarPrefix + this.program;
        }

        private void SetPythonProgramInfo(string svcFullPath)
        {
            if (this.program.EndsWith(".exe"))
            {
                this.processInfo.FileName = svcFullPath;
                return;
            }

            if (this.program.EndsWith(".py"))
            {
                var dependencyFilePath = this.processInfo.WorkingDirectory + Utility.GetFileSeparator() +
                                         HostAgentConstants.PythonDependencyFile;
                if (File.Exists(dependencyFilePath))
                {
                    var strCmdText = "install -r " + dependencyFilePath;
                    var dependencyInstall = Process.Start("pip", strCmdText);
                    while (!dependencyInstall.HasExited)
                    {
                        Console.WriteLine($"Install dependency......");
                        Task.Delay(2000).Wait();
                    }

                    if (dependencyInstall.HasExited && dependencyInstall.ExitCode == 0)
                    {
                        Console.WriteLine($"Install dependency succeed.");
                    }
                    else
                    {
                        Console.WriteLine($"Fail to install dependency. Please check if the requirements.txt file is correct.");
                        throw new InvalidOperationException("Fail to install dependency");
                    }
                }
                else
                {
                    Console.WriteLine($"Fail to find python dependency file: {HostAgentConstants.PythonDependencyFile}. " +
                                      $"Will not install any dependency package. This may cause the service failed if it relies on some external dependencies.");
                }

                this.processInfo.FileName = Utility.GetPythonCommand();
                this.processInfo.Arguments = this.program;
                return;
            }

            this.processInfo.FileName = svcFullPath;
        }
    }
}

