

namespace Microsoft.Telepathy.EchoServer.Services
{
    using System.Threading.Tasks;
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Microsoft.Telepathy.ProtoBuf;

    public class EchoService : Echo.EchoBase
    {
        private readonly ILogger<EchoService> _logger;
        public EchoService(ILogger<EchoService> logger)
        {
            _logger = logger;
        }

        public override async Task<EchoReply> Echo(EchoRequest request, ServerCallContext context)
        {
            await Task.Delay(request.DelayTime);
            return new EchoReply
            {
                Message = request.Message
            };
        }
    }
}
