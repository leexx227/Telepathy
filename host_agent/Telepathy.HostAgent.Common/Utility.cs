using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace Microsoft.Telepathy.HostAgent.Common
{
    public class Utility
    {
        public static void TryGetEnvironmentVariable<T>(string variable, ref T result)
        {
            string value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    Type type = typeof(T);
                    result = (T)Convert.ChangeType(value, type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) ? Nullable.GetUnderlyingType(typeof(T)) : typeof(T));
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[Input Error]: Environment variable: {variable} value: {value} cannot be parse.");
                    Trace.TraceError($"[Input Error]: Environment variable: {variable} value: {value} cannot be parse.");
                }

            }
        }

        public static string GetFileSeparator()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    return HostAgentConstants.WinFilePathSeparator;
                case PlatformID.Unix:
                    return HostAgentConstants.UnixFilePathSeparator;
                default:
                    throw new NotSupportedException("Only support windows and linux OS.");
            }
        }

        public static void PrintProcessInfo(ProcessStartInfo processInfo)
        {
            Console.WriteLine($"FileName: {processInfo.FileName}, Args: {processInfo.Arguments}");
            foreach (var key in processInfo.Environment.Keys)
            {
                if (key.Contains("TELEPATHY"))
                {
                    Console.WriteLine($"Environment Variable {key} = {processInfo.EnvironmentVariables[key]}");
                }
            }
        }

        public static int GetAvailableSvcPort()
        {
            var rdn = new Random();
            while (true)
            {
                int port = rdn.Next(HostAgentConstants.SearchPortStart, UInt16.MaxValue);
                if (PortAvailable(port))
                {
                    Console.WriteLine($"Find available port: {port}.");
                    return port;
                }
            }
        }

        public static bool PortAvailable(int port)
        {
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();
            foreach (var tcpi in tcpConnInfoArray)
            {
                if (tcpi.Port == port)
                {
                    return false;
                }
            }

            return true;
        }

        public static void ThrowIfNullOrEmpty(string arg, string name)
        {
            if (string.IsNullOrEmpty(arg))
            {
                Console.WriteLine($"{name} can't be null or empty.");
                Trace.TraceError($"{name} can't be null or empty.");
                throw new ArgumentException($"{name} value can't be null or empty", name);
            }
        }

        public static string GetPythonCommand()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                    return HostAgentConstants.PythonCommand;
                case PlatformID.Unix:
                    return HostAgentConstants.Python3Command;
                default:
                    throw new NotSupportedException("Only support windows and linux OS.");
            }
        }
    }
}
