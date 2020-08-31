using System;

namespace Microsoft.Telepathy.HostAgent.Common
{
    public class HostAgentConstants
    {
        public const string SvcPortEnvVar = "TELEPATHY_SVC_PORT";
        //public const string SvcHostnameEnvVar = "TELEPATHY_SVC_HOSTNAME";
        //public const string DispatcherPortEnvVar = "TELEPATHY_DISPATCHER_PORT";
        public const string DispatcherIpEnvVar = "TELEPATHY_DISPATCHER_IP";
        public const string SvcTimeoutEnvVar = "TELEPATHY_SVC_TIMEOUT";
        public const string SvcConcurrencyEnvVar = "TELEPATHY_SVC_CONCURRENCY";
        public const string PrefetchCountEnvVar = "TELEPATHY_PREFETCH_COUNT";
        public const string SvcLanguageEnvVar = "TELEPATHY_SVC_LANGUAGE";
        public const string TelepathyWorkingDirEnvVar = "TELEPATHY_WORKING_DIR";
        public const string SvcFullPathEnvVar = "TELEPATHY_SVC_FULL_PATH";
        public const string SessionIdEnvVar = "TELEPATHY_SESSION_ID";
        public const string SvcInitTimeoutEnvVar = "TELEPATHY_SVC_INIT_TIMEOUT";

        public const string CsharpLanguage = "csharp";
        public const string JavaLanguage = "java";
        public const string PythonLanguage = "python";
        public const string JarPrefix = "-jar ";
        public const string DotnetCommand = "dotnet";
        public const string JavaCommand = "java";
        public const string PythonCommand = "python";
        public const string WinFilePathSeparator = @"\";
        public const string UnixFilePathSeparator = @"/";

        public const int searchPortStart = 5001;
    }
}
