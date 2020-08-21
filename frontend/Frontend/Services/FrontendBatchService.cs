using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Microsoft.Telepathy.Frontend.Services
{
    using Microsoft.Telepathy.ProtoBuf;

    public class FrontendBatchService : FrontendBatch.FrontendBatchBase
    {
        private readonly ILogger<FrontendBatchService> _logger;
        public FrontendBatchService(ILogger<FrontendBatchService> logger)
        {
            _logger = logger;
        }

        public override Task<Empty> EndTasks(EndTasksRequest request, ServerCallContext context)
        {
            return base.EndTasks(request, context);
        }

        public override Task GetResults(GetResultsRequest request, IServerStreamWriter<InnerResult> responseStream, ServerCallContext context)
        {
            return base.GetResults(request, responseStream, context);
        }

        public override Task<Empty> SendTask(IAsyncStreamReader<InnerTask> requestStream, ServerCallContext context)
        {
            return base.SendTask(requestStream, context);
        }

        public override Task<Empty> CloseBatch(CloseBatchClientRequest request, ServerCallContext context)
        {
            return base.CloseBatch(request, context);
        }
    }
}
