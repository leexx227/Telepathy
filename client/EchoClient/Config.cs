// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.EchoClient
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The configurations for the EchoClient
    /// </summary>
    public class Config
    {
        private const string HeadNodeArg = "headnode";
        private const string RequestArg = "numberOfRequests";
        private const string TimeMSArg = "timeMS";
        private const string SizeByteArg = "sizeByte";
        private const string WarmupSecArg = "warmupSec";
        private const string DurableArg = "durable";
        private const string PriorityArg = "priority";
        private const string UserNameArg = "username";
        private const string PasswordArg = "password";
        private const string InsecureArg = "insecure";
        private const string ServiceNameArg = "serviceName";
        private const string ShareSessionArg = "shareSession";
        private const string SessionPoolArg = "sessionPool";
        private const string RuntimeArg = "runtime";
        private const string JobNameArg = "jobName";
        private const string EnvironmentArg = "environment";
        private const string VerboseArg = "verbose";
        private const string SizeKBRandomArg = "sizeKBRandom";
        private const string TimeMSRandomArg = "timeMSRandom";
        private const string MsgTimeoutSecArg = "msgTimeoutSec";
        private const string ParentJobIdsArg = "parentIds";
        private const string ServiceIdleSecArg = "serviceIdleSec";
        private const string ServiceHangSecArg = "serviceHangSec";


        private bool helpInfo = false;
        public bool HelpInfo
        {
            get { return this.helpInfo; }
        }
        private string headNode = "localhost";
        public string HeadNode
        {
            get { return this.headNode; }
        }
        private int numberOfRequest = 10;
        public int NumberOfRequest
        {
            get { return this.numberOfRequest; }
        }
        private int callDurationMS = 0;
        public int CallDurationMS
        {
            get { return this.callDurationMS; }
        }
        private long messageSizeByte = 0;
        public long MessageSizeByte
        {
            get { return this.messageSizeByte; }
        }

        private int warmupTimeSec = 0;
        public int WarmupTimeSec
        {
            get { return this.warmupTimeSec; }
        }
        private int priority = 2000;
        public int Priority
        {
            get { return this.priority; }
        }
        private string username;
        public string Username
        {
            get { return this.username; }
        }
        private string password;
        public string Password
        {
            get { return this.password; }
        }
        private bool durable = false;
        public bool Durable
        {
            get { return this.durable; }
        }

        private bool insecure = false;
        public bool Insecure
        {
            get { return this.insecure; }
        }

        private bool shareSession = false;

        public bool ShareSession
        {
            get { return this.shareSession; }
        }
        private bool sessionPool = false;
        public bool SessionPool
        {
            get { return this.sessionPool; }
        }
        private int runtime = -1;
        public int Runtime
        {
            get { return this.runtime; }
        }
        private string jobName = string.Empty;
        public string JobName
        {
            get { return this.jobName; }
        }
        private string environment = string.Empty;
        public string Environment
        {
            get { return this.environment; }
        }

        private string serviceName = "Echo";
        public string ServiceName
        {
            get { return this.serviceName; }
        }
        private bool verbose = false;
        public bool Verbose
        {
            get { return this.verbose; }
        }
        private int flush = 0;
        public int Flush
        {
            get { return this.flush; }
        }
        private string sizeKBRandom = string.Empty;
        public string SizeKBRandom
        {
            get { return this.sizeKBRandom; }
        }
        private string timeMSRandom = string.Empty;
        public string TimeMSRandom
        {
            get { return this.timeMSRandom; }
        }
        private int msgTimeoutSec = 60 * 60; //by default, the message operation timeout is 60 minutes
        public int MsgTimeoutSec
        {
            get { return this.msgTimeoutSec; }
        }
        private string parentIds = string.Empty;
        public string ParentIds
        {
            get { return this.parentIds; }
        }
        private int? serviceIdleSec = null; //by default, the service idle timeout depends on that in service registration
        public int? ServiceIdleSec
        {
            get { return this.serviceIdleSec; }
        }
        private int? serviceHangSec = null; //by default, the service hang timeout depends on that in service registration
        public int? ServiceHangSec
        {
            get { return this.serviceHangSec; }
        }


        public Config(CmdParser parser)
        {
            this.helpInfo = parser.GetSwitch("?") || parser.GetSwitch("help");
            parser.TryGetArg<string>(HeadNodeArg, ref this.headNode);
            parser.TryGetArg<int>(RequestArg, ref this.numberOfRequest);
            parser.TryGetArg<int>(TimeMSArg, ref this.callDurationMS);
            parser.TryGetArg<long>(SizeByteArg, ref this.messageSizeByte);
            parser.TryGetArg<int>(WarmupSecArg, ref this.warmupTimeSec);
            parser.TryGetArg<int>(PriorityArg, ref this.priority);
            parser.TryGetArg<string>(UserNameArg, ref this.username);
            parser.TryGetArg<string>(PasswordArg, ref this.password);
            parser.TryGetArg<int>(RuntimeArg, ref this.runtime);
            parser.TryGetArg<string>(JobNameArg, ref this.jobName);
            parser.TryGetArg<string>(EnvironmentArg, ref this.environment);
            parser.TryGetArg<string>(ServiceNameArg, ref this.serviceName);
            parser.TryGetArg<string>(TimeMSRandomArg, ref this.timeMSRandom);
            parser.TryGetArg<string>(SizeKBRandomArg, ref this.sizeKBRandom);
            parser.TryGetArg<int>(MsgTimeoutSecArg, ref this.msgTimeoutSec);
            parser.TryGetArg<string>(ParentJobIdsArg, ref this.parentIds);
            parser.TryGetArg<int?>(ServiceIdleSecArg, ref this.serviceIdleSec);
            parser.TryGetArg<int?>(ServiceHangSecArg, ref this.serviceHangSec);

            this.insecure = parser.GetSwitch(InsecureArg);
            this.durable = parser.GetSwitch(DurableArg);
            this.shareSession = parser.GetSwitch(ShareSessionArg);
            this.sessionPool = parser.GetSwitch(SessionPoolArg);
            this.verbose = parser.GetSwitch(VerboseArg);
        }

        public void PrintHelp()
        {
            Console.WriteLine();
            Console.WriteLine("Usage: EchoClient.exe -headnode <HeadNode> -jobName <JobName> -serviceName <ServiceName> -numberOfRequests <10> -timeMS <0> -sizeByte <0> -resourceType:<core|node|socket|gpu> -min <N> -max <N> -priority <N> -jobTemplate <templateA> -environment <Environment> -username <Username> -password <Password> -azureQueue <True|False> -runtime <N Sec> -timeMSRandom <N>_<N> -sizeKBRandom <N>_<N> -msgTimeoutSec <N> -parentIds <id,id,...> -serviceIdleSec <N> -serviceHangSec <N>  -durable -insecure -shareSession -sessionPool -verbose");
            Console.WriteLine();
            Console.WriteLine("Usage: EchoClient.exe /headnode:<HeadNode> /jobName:<JobName> /serviceName:<ServiceName> /numberOfRequests:<10> /timeMS:<0> /sizeByte:<0> /resourceType:<core|node|socket|gpu> /min:<N> /max:<N> /priority:<N> /jobTemplate:<templateA> /environment:<Environment> /username:<Username> /password:<Password> /azureQueue:<True|False> /runtime:<N Sec> /timeMSRandom:<N>_<N> /sizeKBRandom:<N>_<N> /msgTimeoutSec:<N> /parentIds:<id,id,...> /serviceIdleSec:<N> /serviceHangSec:<N>  /durable /insecure /shareSession /sessionPool /verbose");
            Console.WriteLine();
            Console.WriteLine("Sample: EchoClient.exe");
            Console.WriteLine("Sample: EchoClient.exe -h HeadNode -n 20");
            Console.WriteLine("Sample: EchoClient.exe -h HeadNode -n 50 -max 10 -v");
            Console.WriteLine("Sample: EchoClient.exe -h HeadNode -n 100 -time 5 -size 10 -max 20 -durable");
        }

        public bool PrintUsedParams(CmdParser parser)
        {
            bool used = false;
            Dictionary<string, string> usedArgs;
            List<string> usedSwitches;
            parser.Used(out usedArgs, out usedSwitches);
            if (usedArgs != null && usedArgs.Count > 0)
            {
                foreach (var kv in usedArgs)
                {
                    Console.WriteLine("Parameter : {0}=\"{1}\"", kv.Key, kv.Value);
                }
                used = true;
            }
            if (usedSwitches != null && usedSwitches.Count > 0)
            {
                foreach (string s in usedSwitches)
                {
                    Console.WriteLine("Switch : -{0}", s);
                }
                used = true;
            }
            return used;
        }

        public bool PrintUnusedParams(CmdParser parser)
        {
            bool anyUnused = false;
            Dictionary<string, string> unusedArgs;
            List<string> unusedSwitches;
            parser.Unused(out unusedArgs, out unusedSwitches);
            ConsoleColor prevFGColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            if (unusedArgs != null && unusedArgs.Count > 0)
            {
                foreach (var kv in unusedArgs)
                {
                    Console.WriteLine("Error : Unidentified parameter {0}=\"{1}\"", kv.Key, kv.Value);
                }
                anyUnused = true;
            }
            if (unusedSwitches != null && unusedSwitches.Count > 0)
            {
                foreach (string s in unusedSwitches)
                {
                    Console.WriteLine("Error : Unidentified switch -{0}", s);
                }
                anyUnused = true;
            }
            Console.ForegroundColor = prevFGColor;
            return anyUnused;
        }
    }
}
