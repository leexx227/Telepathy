using System;
using System.Threading.Tasks;
using Microsoft.Telepathy.ClientAPI;
using Microsoft.Telepathy.ProtoBuf;
using Session = Microsoft.Telepathy.ClientAPI.Session;

namespace Microsoft.Telepathy.EchoClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            CmdParser parser = new CmdParser(args);
            Config config = new Config(parser);

            if (config.HelpInfo)
            {
                config.PrintHelp();
                return;
            }

            if (config.Verbose)
            {
                config.PrintUsedParams(parser);
            }

            if (config.PrintUnusedParams(parser))
            {
                config.PrintHelp();
                return;
            }

            SessionStartInfo info = new SessionStartInfo(config.HeadNode, config.ServiceName);

            Session session = null;

            try
            {
                using (session = await Session.CreateSession(info))
                {
                    using (var client = new BatchClient(Guid.NewGuid().ToString(), session, Echo.Descriptor.FindMethodByName("Echo")))
                    {
                        for (int i = 0; i < config.NumberOfRequest; i++)
                        {
                            client.SendTask(new EchoRequest(null, config.MessageSizeByte, config.CallDurationMS));
                        }

                        await client.EndTasks();

                        var result = await client.GetResults<EchoReply>();
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                if (session != null)
                {
                    await session.Close();
                    session.Dispose();
                }
            }
        }
    }
}
