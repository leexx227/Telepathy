
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Microsoft.Telepathy.ClientAPI
{
    internal interface IBatchClient : IDisposable
    {
        void Close();

        void SendTask(IMessage message);

        Task EndTasks();

        Task<IEnumerable<TResponse>> GetResults<TResponse>();
    }
}