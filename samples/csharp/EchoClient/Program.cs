using System;
using System.Threading.Tasks;
using Microsoft.Telepathy.ClientAPI;
using Microsoft.Telepathy.ProtoBuf;

namespace EchoClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            SessionStartInfo info = new SessionStartInfo("https://localhost:5005", "Echo");

            using (Session session = await Session.CreateSessionAsync(info))
            {
                Console.WriteLine("Session {0} has been created", session.Id);

                using (var client = new BatchClient(session, Echo.Descriptor.FindMethodByName("Echo")))
                {
                    //send task
                    var request = new EchoRequest{ Message = "hello" };
                    client.SendTask(request);
                    await client.EndTasksAsync();

                    //get result
                    await foreach (var result in client.GetResultsAsync<EchoReply>())
                    {
                        Console.WriteLine("Echo hello, and we get {0} from service", result.Message);
                    }
                }

                await session.CloseAsync();
            }

            Console.WriteLine("Done invoking Echo service");

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
