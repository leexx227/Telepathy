using System;

namespace Microsoft.Telepathy.HostAgent.Common
{
    public class HostAgentConstants
    {
        public const string SvcPortVariable = "SVC_PORT";
        public const string SvcHostnameVariable = "SVC_HOSTNAME";
        public const string DispatcherPortVariable = "DISPATCHER_PORT";
        public const string DispatcherIpVariable = "DISPATCHER_IP";
        public const string SvcTimeoutVariable = "SVC_TIMEOUT";
        public const string SvcConcurrencyVariable = "SVC_CONCURRENCY";
        public const string PrefetchCountVariable = "PREFETCH_COUNT";
        public const string SvcLanguageVariable = "SVC_LANGUAGE";
        public const string SvcOSVariable = "SVC_OS";
        public const string HostAgentWorkingDirVariable = "HOSTAGENT_WORKINGDIR";
        public const string SvcWorkingDirVariable = "SVC_WORKINGDIR";
        public const string SvcProgramNameVariable = "SVC_PROGRAM_NAME";
        public const string SessionIdVariable = "SESSION_ID";

        public const string CsharpLanguage = "csharp";
        public const string JavaLanguage = "java";
        public const string PythonLanguage = "python";
        public const string JarPrefix = "-jar ";
        public const string DotnetCommand = "dotnet";
        public const string JavaCommand = "java";
        public const string PythonCommand = "python";
    }
}
