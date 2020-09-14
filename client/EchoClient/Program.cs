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
            info.MaxServiceNum = 1;
            Session session = null;

            try
            {
                using (session = await Session.CreateSessionAsync(info))
                {
                    using (var client = new BatchClient(Guid.NewGuid().ToString(), session, Echo.Descriptor.FindMethodByName("Echo")))
                    {
                        for (int i = 0; i < config.NumberOfRequest; i++)
                        {
                            client.SendTask(new EchoRequest(i.ToString(), config.MessageSizeByte, config.CallDurationMS));
                        }

                        await client.EndTasksAsync();

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
                    await session.CloseAsync();
                    session.Dispose();
                }
            }
        }
    }
}
