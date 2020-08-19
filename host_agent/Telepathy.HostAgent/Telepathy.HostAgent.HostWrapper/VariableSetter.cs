using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Telepathy.HostAgent.Common;

namespace Microsoft.Telepathy.HostAgent.HostWrapper
{
    class VariableSetter
    {
        private List<string> mustNotNullVariableList;

        public VariableSetter(List<string> mustVariableList)
        {
            this.mustNotNullVariableList = mustVariableList;
        }

        public string GetEnvironmentVariable(string variable)
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrEmpty(value))
            {
                Console.WriteLine($"Get task environment :{variable}, value: {value}");
                Trace.TraceInformation($"Get task environment :{variable}, value: {value}");
                return value;
            }
            else
            {
                if (this.mustNotNullVariableList.Contains(variable))
                {
                    Console.WriteLine($"Get task environment variable failed. {variable} must be config.");
                    Trace.TraceError($"Get task environment variable failed. {variable} must be config.");
                    throw new InvalidOperationException($"Get environment variable failed. {variable} must be config.");
                }
                else
                {
                    return null;
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
    }
}
