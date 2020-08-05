using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Telepathy.HostAgent.Common
{
    public class RetryManager
    {
        public delegate Task<TResult> OnAction<TResult>();

        public delegate Task OnException(Exception e);

        public const int InfiniteRetries = -1;

        private TimeSpan currentWaitTime;

        private TimeSpan nextWaitTime;

        private int maxRetries;

        private TimeSpan defaultRetryInterval;

        private bool isIncrementalRetry;

        private int retryCount;

        public int RetryCount
        {
            get { return retryCount;}
            private set { this.retryCount = value; }
        }

        public bool HasAttemptsLeft =>
            (this.maxRetries == InfiniteRetries || this.RetryCount < this.maxRetries);

        private int waitIncrementalFactor = 2;

        public RetryManager(TimeSpan retryInterval, int maxRetries) : this(retryInterval, maxRetries, false)
        {

        }

        public RetryManager(TimeSpan retryInterval, int maxRetries, bool isIncrementalRetry)
        {
            this.defaultRetryInterval = retryInterval;
            this.maxRetries = maxRetries;
            this.isIncrementalRetry = isIncrementalRetry;
            this.RetryCount = 0;

            this.currentWaitTime = TimeSpan.Zero;
            this.nextWaitTime = this.defaultRetryInterval;
        }

        public async Task<TResult> RetryOperationAsync<TResult>(OnAction<TResult> action, OnException onException)
        {
            TResult result = default(TResult);
            while (true)
            {
                try
                {
                    result = await action();
                    Reset();
                    return result;
                }
                catch (Exception e)
                {
                    if (this.HasAttemptsLeft)
                    {
                        await onException(e);
                        await this.WaitForNextAttempt();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        public async Task<TResult> RetryOperationWithKnownExceptionAsync<TResult>(OnAction<TResult> action, OnException onException, Func<Exception, bool> exPredicate)
        {
            var currentRetryCount = 0;
            TResult result = default(TResult);
            while (true)
            {
                try
                {
                    result = await action();
                    Reset();
                    return result;
                }
                catch (Exception ex)
                {
                    if (exPredicate(ex))
                    {
                        Trace.TraceInformation("{0}:{1} RetryCount: {2} \n",
                        ex.GetType(),
                        ex.Message,
                        currentRetryCount);
                        await this.WaitForNextAttempt();
                    }
                    else
                    {
                        if (this.HasAttemptsLeft)
                        {
                            await onException(ex);
                            await this.WaitForNextAttempt();
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }
        }

        public async Task WaitForNextAttempt()
        {
            if (!this.HasAttemptsLeft)
            {
                throw new InvalidOperationException("There are no more retry attempts remaining");
            }

            this.currentWaitTime = this.nextWaitTime;
            this.nextWaitTime = this.GetNextWaitTime();

            Interlocked.Increment(ref this.retryCount);

            Debug.Assert(this.currentWaitTime.Milliseconds >= 0);
            await Task.Delay(this.currentWaitTime);
        }

        public TimeSpan GetNextWaitTime()
        {
            if (!this.isIncrementalRetry)
            {
                return this.defaultRetryInterval;
            }
            else
            {
                return TimeSpan.FromMilliseconds(this.currentWaitTime.Milliseconds * this.waitIncrementalFactor);
            }
        }

        public void Reset()
        {
            this.RetryCount = 0;
            this.currentWaitTime = TimeSpan.Zero;
            this.nextWaitTime = this.defaultRetryInterval;
        }
    }
}
