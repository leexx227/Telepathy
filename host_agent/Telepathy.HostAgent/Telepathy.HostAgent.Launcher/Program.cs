using System;
using Microsoft.Telepathy.HostAgent.Core;
using Microsoft.Telepathy.HostAgent.Interface;

namespace Telepathy.HostAgent.Launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            var parser = new EnvironmentParser();
            var environmentInfo = new EnvironmentInfo(parser);

            IHostAgent hostAgent = new Microsoft.Telepathy.HostAgent.Core.HostAgent(environmentInfo);

            hostAgent.StartAsync().GetAwaiter().GetResult();

            Console.ReadKey();
        }
    }
}
