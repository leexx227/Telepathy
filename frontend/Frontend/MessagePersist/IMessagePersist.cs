using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Telepathy.ProtoBuf;

namespace Microsoft.Telepathy.Frontend.MessagePersist
{
    public interface IMessagePersist : IDisposable
    {
        Task PutTaskAsync(InnerTask task);

        Task<InnerResult> GetResultAsync();
    }
}
