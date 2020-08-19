using Microsoft.Telepathy.HostAgent.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace Microsoft.Telepathy.HostAgent.HostWrapper
{
    public class WrapperUtility
    {
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
                Console.WriteLine($"Environment Variable {key} = {processInfo.EnvironmentVariables[key]}");
            }
        }

        public static bool CheckEnvVarValidation(List<string> mustNotNullVarList)
        {
            foreach (var variable in mustNotNullVarList)
            {
                var value = Environment.GetEnvironmentVariable(variable);
                if (string.IsNullOrEmpty(value))
                {
                    Console.WriteLine($"Environment variable {variable} can't be null.");
                    Trace.TraceError($"Environment variable {variable} can't be null.");
                    return false;
                }
            }

            return true;
        }
    }
}
