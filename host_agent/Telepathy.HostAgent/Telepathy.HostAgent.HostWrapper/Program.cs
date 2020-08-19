using Microsoft.Telepathy.HostAgent.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.Telepathy.HostAgent.HostWrapper
{
    class Program
    {
        private static Process svcProcess;
        private static Process hostAgentProcess;
        static void Main(string[] args)
        {
            SetEnvironmentVariable();
            var hostAgentLoader = new HostAgentLoader(GetHostAgentMustVariableList());
            var svcLoader = new SvcLoader(GetSvcMustVariableList());
            while (true)
            {
                var svcPort = GetAvailableSvcPort();
                try
                {
                    svcProcess = svcLoader.LoadSvc(svcPort);
                    if (!svcProcess.HasExited)
                    {
                        hostAgentProcess = hostAgentLoader.LoadSvc(svcPort);
                        break;
                    }
                    else
                    {
                        if (!PortAvailable(svcPort))
                        {
                            Trace.TraceInformation($"Find port: {svcPort} not available. Continue to search available port.");
                            Console.WriteLine($"Find port: {svcPort} not available. Continue to search available port.");
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!PortAvailable(svcPort))
                    {
                        Trace.TraceInformation($"Find port: {svcPort} not available. Continue to search available port.");
                        Console.WriteLine($"Find port: {svcPort} not available. Continue to search available port.");
                    }
                    else
                    {
                        Trace.TraceError($"Error occured: {e.Message}");
                        Console.WriteLine($"Error occured: {e.Message}");
                        throw;
                    }
                }
            }

            if (svcProcess.HasExited || hostAgentProcess.HasExited)
            {
                var exitedProcess = svcProcess.HasExited ? "Service" : "Host agent";
                Trace.TraceError($"Error occured. {exitedProcess} process exited.");
                Console.WriteLine($"Error occured. {exitedProcess} process exited.");
                throw new Exception($"Error occured. {exitedProcess} process exited.");
            }
            Console.ReadKey();
        }

        static string GetAvailableSvcPort()
        {
            return "5001";
        }

        static bool PortAvailable(string port)
        {
            return true;
        }

        static List<string> GetHostAgentMustVariableList()
        {
            return new List<string>()
            {
                HostAgentConstants.DispatcherIpEnvVar, HostAgentConstants.TelepathyWorkingDirEnvVar,
                HostAgentConstants.SessionIdEnvVar
            };
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

        static List<string> GetSvcMustVariableList()
        {
            return new List<string>()
            {
                HostAgentConstants.SvcLanguageEnvVar, HostAgentConstants.TelepathyWorkingDirEnvVar,
                HostAgentConstants.SvcFullPathEnvVar
            };
        }
    }
}
