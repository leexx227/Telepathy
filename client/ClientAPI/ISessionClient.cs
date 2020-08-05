#region

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;

#endregion

namespace Microsoft.Telepathy.ClientAPI
{
    internal interface ISessionClient : IDisposable
    {
        void Close();

        void SendRequest(IMessage message);

        Task EndRequests();

        Task<IEnumerable<TResponse>> GetResponses<TResponse>();
    }
}