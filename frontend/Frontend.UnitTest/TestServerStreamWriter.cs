using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Grpc.Core;

namespace Frontend.UnitTest
{
    class TestServerStreamWriter<T> : IServerStreamWriter<T>
    {
        public List<T> Responses { get; } = new List<T>();

        public Task WriteAsync(T message)
        {
            this.Responses.Add(message);
            return Task.CompletedTask;
        }

        public WriteOptions WriteOptions { get; set; }
    }
}
