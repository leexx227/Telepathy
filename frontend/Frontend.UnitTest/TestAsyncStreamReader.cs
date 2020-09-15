using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;

namespace Frontend.UnitTest
{
    class TestAsyncStreamReader<T> : IAsyncStreamReader<T>
    {
        public TestAsyncStreamReader(T current)
        {
            Current = current;
        }

        public void Dispose()
        {
        }

        private bool _hasNext = true;

        public Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            var result = Task.FromResult(_hasNext);
            _hasNext = false;
            return result;
        }

        public T Current { get; private set; }
    }
}
