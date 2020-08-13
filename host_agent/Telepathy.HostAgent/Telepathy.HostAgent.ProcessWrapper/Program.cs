using Microsoft.Telepathy.HostAgent.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Telepathy.HostAgent.ProcessWrapper
{
    class Program
    {
        static void Main(string[] args)
        {
            SetEnvironmentVariable();
            LoadHostAgent();
            LoadSvc();
            Console.ReadKey();
        }

        static void LoadHostAgent()
        {
            var mustNotNullVariableList = GetHostAgentMustVariableList();
            var setter = new EnvironmentSetter(mustNotNullVariableList);

            var cmd = "dotnet";
            var workingDirectory = setter.GetEnvironmentVariable(HostAgentConstants.HostAgentWorkingDirVariable);
            var program = @"Microsoft.Telepathy.HostAgent.Launcher.dll";

            var processInfo = new ProcessStartInfo(cmd, program);
            processInfo.WorkingDirectory = workingDirectory;

            processInfo.EnvironmentVariables[HostAgentConstants.SvcHostnameVariable] = setter.GetEnvironmentVariable(HostAgentConstants.SvcHostnameVariable);

            processInfo.EnvironmentVariables[HostAgentConstants.SvcPortVariable] = setter.GetEnvironmentVariable(HostAgentConstants.SvcPortVariable);

            processInfo.EnvironmentVariables[HostAgentConstants.DispatcherIpVariable] = setter.GetEnvironmentVariable(HostAgentConstants.DispatcherIpVariable);

            processInfo.EnvironmentVariables[HostAgentConstants.DispatcherPortVariable] = setter.GetEnvironmentVariable(HostAgentConstants.DispatcherPortVariable);

            processInfo.EnvironmentVariables[HostAgentConstants.SvcTimeoutVariable] = setter.GetEnvironmentVariable(HostAgentConstants.SvcTimeoutVariable);

            processInfo.EnvironmentVariables[HostAgentConstants.SvcConcurrencyVariable] = setter.GetEnvironmentVariable(HostAgentConstants.SvcConcurrencyVariable);

            processInfo.EnvironmentVariables[HostAgentConstants.PrefetchCountVariable] = setter.GetEnvironmentVariable(HostAgentConstants.PrefetchCountVariable);

            processInfo.EnvironmentVariables[HostAgentConstants.SessionIdVariable] = setter.GetEnvironmentVariable(HostAgentConstants.SessionIdVariable);

            var process = Process.Start(processInfo);
        }

        static List<string> GetHostAgentMustVariableList()
        {
            return new List<string>(){HostAgentConstants.SvcPortVariable, HostAgentConstants.DispatcherIpVariable, HostAgentConstants.DispatcherPortVariable, 
                HostAgentConstants.HostAgentWorkingDirVariable, HostAgentConstants.SessionIdVariable};
        }

        static void SetEnvironmentVariable()
        {
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcPortVariable, "5001");
            Environment.SetEnvironmentVariable(HostAgentConstants.DispatcherIpVariable, "localhost");
            Environment.SetEnvironmentVariable(HostAgentConstants.DispatcherPortVariable, "5000");
            Environment.SetEnvironmentVariable(HostAgentConstants.HostAgentWorkingDirVariable, @"../../../../Telepathy.HostAgent.Launcher/bin/Debug/netcoreapp3.1");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcLanguageVariable, "csharp");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcOSVariable, "windows");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcWorkingDirVariable, @"E:/SampleTest/LoadGrpcServer/packages/server/csharp");
            Environment.SetEnvironmentVariable(HostAgentConstants.SvcProgramNameVariable, @"GreeterServer.dll");
            Environment.SetEnvironmentVariable(HostAgentConstants.SessionIdVariable, "123456");

            Environment.SetEnvironmentVariable(HostAgentConstants.SvcConcurrencyVariable, "3");
            Environment.SetEnvironmentVariable(HostAgentConstants.PrefetchCountVariable, "10");
        }

        static void LoadSvc()
        {
            var mustNotNullVariableList = GetSvcMustVariableList();
            var setter = new EnvironmentSetter(mustNotNullVariableList);

            string cmd = string.Empty;
            string workingDirectory = setter.GetEnvironmentVariable(HostAgentConstants.SvcWorkingDirVariable);
            string program = string.Empty;
            GetStartInfoArgs(setter, ref cmd, ref program);

            var processInfo = new ProcessStartInfo(cmd, program);
            processInfo.WorkingDirectory = workingDirectory;

            processInfo.EnvironmentVariables[HostAgentConstants.SvcPortVariable] = setter.GetEnvironmentVariable(HostAgentConstants.SvcPortVariable);

            var process = Process.Start(processInfo);
        }

        static void GetStartInfoArgs(EnvironmentSetter setter, ref string cmd, ref string program)
        {
            program = setter.GetEnvironmentVariable(HostAgentConstants.SvcProgramNameVariable);
            var language = setter.GetEnvironmentVariable(HostAgentConstants.SvcLanguageVariable);
            var os = setter.GetEnvironmentVariable(HostAgentConstants.SvcOSVariable);

            switch (language.ToLower())
            {
                case HostAgentConstants.CsharpLanguage:
                    cmd = HostAgentConstants.DotnetCommand;
                    break;
                case HostAgentConstants.JavaLanguage:
                    cmd = HostAgentConstants.JavaCommand;
                    program = HostAgentConstants.JarPrefix + program;
                    break;
                case HostAgentConstants.PythonLanguage:
                    cmd = HostAgentConstants.PythonCommand;
                    break;
                default:
                    Console.WriteLine($"Only support csharp, java and python. Current language config: {language}.");
                    Trace.TraceError($"Only support csharp, java and python. Current language config: {language}.");
                    throw new InvalidOperationException($"Only support csharp, java and python.");
            }
        }

        static List<string> GetSvcMustVariableList()
        {
            return new List<string>() {HostAgentConstants.SvcPortVariable, HostAgentConstants.SvcLanguageVariable, HostAgentConstants.SvcOSVariable, 
                HostAgentConstants.SvcWorkingDirVariable, HostAgentConstants.SvcProgramNameVariable};
        }
    }
}
