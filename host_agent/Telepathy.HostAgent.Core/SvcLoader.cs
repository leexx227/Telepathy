// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace Microsoft.Telepathy.HostAgent.Core
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;

    using Microsoft.Telepathy.HostAgent.Common;

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
            var svcPath = Environment.GetEnvironmentVariable(HostAgentConstants.SvcPathEnvVar);
            Utility.ThrowIfNullOrEmpty(svcPath, HostAgentConstants.SvcPathEnvVar);

            var workingDirEnvVarName = Environment.GetEnvironmentVariable(HostAgentConstants.TelepathyWorkingDirEnvVar);
            Utility.ThrowIfNullOrEmpty(workingDirEnvVarName, HostAgentConstants.TelepathyWorkingDirEnvVar);

            var svcRealPath = Environment.GetEnvironmentVariable(workingDirEnvVarName);
            Utility.ThrowIfNullOrEmpty(svcRealPath, "service real working path");

            var svcFullPath = Path.Combine(svcRealPath, svcPath);

            var pathList = svcPath.Contains(HostAgentConstants.WinFilePathSeparator)
                ? svcPath.Split(HostAgentConstants.WinFilePathSeparator)
                : svcPath.Split(HostAgentConstants.UnixFilePathSeparator);
            this.program = pathList[pathList.Length-1];

            var workingDir = Path.Combine(svcRealPath, Path.Combine(pathList[0..(pathList.Length - 1)]));
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
                var dependencyFilePath = Path.Combine(this.processInfo.WorkingDirectory, HostAgentConstants.PythonDependencyFile);
                if (File.Exists(dependencyFilePath))
                {
                    var strCmdText = "install -r " + dependencyFilePath;
                    var dependencyInstall = Process.Start("pip3", strCmdText);
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
                    Console.WriteLine($"Fail to find python dependency file: {HostAgentConstants.PythonDependencyFile} in path: {dependencyFilePath}. " +
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

