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
            AppContext.SetSwitch(
                "System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
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
            info.MaxServiceNum = config.MaxServiceNum;
            Session session = null;

            Console.WriteLine("[{0}] Start creating session", DateTime.UtcNow);

            try
            {
                using (session = await Session.CreateSessionAsync(info))
                {
                    Console.WriteLine("[{0}] Session created, id = {1}", DateTime.UtcNow, session.Id);

                    using (var client = new BatchClient(Guid.NewGuid().ToString(), session, Echo.Descriptor.FindMethodByName("Echo")))
                    {
                        Console.WriteLine("[{0}] Start sending {1} tasks", DateTime.UtcNow, config.NumberOfRequest);
                        for (int i = 0; i < config.NumberOfRequest; i++)
                        {
                            client.SendTask(new EchoRequest(i.ToString(), config.MessageSizeByte, config.CallDurationMS));
                        }

                        Console.WriteLine("[{0}] End {1} tasks", DateTime.UtcNow, config.NumberOfRequest);
                        await client.EndTasksAsync();

                        Console.WriteLine("[{0}] Start retrieve results", DateTime.UtcNow);
                        await foreach (var result in client.GetResultsAsync<EchoReply>())
                        {
                            Console.WriteLine("[{0}] {1}", DateTime.UtcNow, result.Message);
                        }
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
                    Console.WriteLine("[{0}] Closing session {1}.", DateTime.UtcNow, session.Id);
                    await session.CloseAsync();
                    session.Dispose();
                }
            }
        }
    }
}
