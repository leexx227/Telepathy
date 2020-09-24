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
            // Service path in service registry file. Should be like "echo/EchoServer.dll".
            // It's the relative path to the real working directory
            var svcPath = Environment.GetEnvironmentVariable(HostAgentConstants.SvcPathEnvVar);
            Utility.ThrowIfNullOrEmpty(svcPath, HostAgentConstants.SvcPathEnvVar);

            // Working directory environment variable name. The real working directory should be red from "workingDirEnvVarName".
            // Use this environment variable in order to hide the underlying resource provider from host agent.
            var workingDirEnvVarName = Environment.GetEnvironmentVariable(HostAgentConstants.TelepathyWorkingDirEnvVar);
            Utility.ThrowIfNullOrEmpty(workingDirEnvVarName, HostAgentConstants.TelepathyWorkingDirEnvVar);

            // Real working directory read from environment variable.
            var svcRealPath = Environment.GetEnvironmentVariable(workingDirEnvVarName);
            Utility.ThrowIfNullOrEmpty(svcRealPath, "service real working path");

            // The absolute path of the service.
            var svcFullPath = Path.Combine(svcRealPath, svcPath);

            // In order to set the working directory in process start info, service program file and the path should be split.
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

            // Use "dotnet <service_name>.dll" to load csharp service on both Windows and Linux.
            if (this.program.EndsWith(".dll"))
            {
                this.processInfo.FileName = HostAgentConstants.DotnetCommand;
                this.processInfo.Arguments = this.program;
                return;
            }

            // Support Linux executable file.
            this.processInfo.FileName = svcFullPath;
        }

        private void SetJavaProgramInfo()
        {
            // Use "java -jar <service_name>.jar" to load java service.
            this.processInfo.FileName = HostAgentConstants.JavaCommand;
            this.processInfo.Arguments = HostAgentConstants.JarPrefix + this.program;
        }

        private void SetPythonProgramInfo(string svcFullPath)
        {
            // Support Windows executable file (Not recommended as some third-party executable file generation tools
            // may cause security problem on Windows Server and may be delete by the system automatically).
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
                    // Use "pip3 install -r requirement.txt" to install the external python packages.
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

                // The python command on Windows is "python" with version 3.8.
                // The python command on ubuntu 18.04 is "python3" with version 3.6 which is pre-installed and the
                // default "python" command on ubuntu 18.04 is python2.7 which is also pre-installed.
                this.processInfo.FileName = Utility.GetPythonCommand();
                this.processInfo.Arguments = this.program;
                return;
            }

            // Support Linux executable file (Not recommended).
            this.processInfo.FileName = svcFullPath;
        }
    }
}

