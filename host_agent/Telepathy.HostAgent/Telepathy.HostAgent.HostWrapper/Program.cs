using Microsoft.Telepathy.HostAgent.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;

namespace Microsoft.Telepathy.HostAgent.HostWrapper
{
    class Program
    {
        private static Process svcProcess;
        private static Process hostAgentProcess;
        private static int portStart = 5001;
        private static int maxRetries = 5;

        static void Main(string[] args)
        {
            SetEnvironmentVariable();
            var hostAgentLoader = new HostAgentLoader(GetHostAgentMustVariableList());
            var svcLoader = new SvcLoader(GetSvcMustVariableList());
            var currentRetryCount = 0;
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
                        else
                        {
                            currentRetryCount++;
                            Trace.TraceError($"Error occured when loading service. Retry count: {currentRetryCount}");
                            Console.WriteLine($"Error occured when loading service. Retry count: {currentRetryCount}");
                            if (currentRetryCount >= maxRetries)
                            {
                                Trace.TraceError($"Error occured when loading service. Retry count exhausted.");
                                Console.WriteLine($"Error occured when loading service. Retry count exhausted.");
                                throw new Exception($"Error occured when loading service.");
                            }
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
                        currentRetryCount++;
                        Trace.TraceError($"Error occured: {e.Message}. Retry count: {currentRetryCount}");
                        Console.WriteLine($"Error occured: {e.Message}. Retry count: {currentRetryCount}");
                        if (currentRetryCount >= maxRetries)
                        {
                            Trace.TraceError($"Error occured: {e.Message}. Retry count exhausted.");
                            Console.WriteLine($"Error occured: {e.Message}. Retry count exhausted.");
                            throw;
                        }
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

        static int GetAvailableSvcPort()
        {
            var rdn = new Random();
            while (true)
            {
                int port = rdn.Next(portStart, UInt16.MaxValue);
                if (PortAvailable(port))
                {
                    Console.WriteLine($"Find available port: {port}.");
                    return port;
                }
            }
        }

        static bool PortAvailable(int port)
        {
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
            foreach (var tcpi in tcpConnInfoArray)
            {
                if (tcpi.LocalEndPoint.Port == port)
                {
                    return false;
                }
            }

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

        static void StartSvc(HostAgentLoader hostAgentLoader, SvcLoader svcLoader)
        {

        }
    }
}
