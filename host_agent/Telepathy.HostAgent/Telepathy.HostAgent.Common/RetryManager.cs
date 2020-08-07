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

        private int currentWaitTimeMs;

        private int nextWaitTimeMs;

        private int maxRetries;

        private int defaultRetryIntervalMs;

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

        public RetryManager(int retryIntervalMs, int maxRetries) : this(retryIntervalMs, maxRetries, false)
        {

        }

        public RetryManager(int retryIntervalMs, int maxRetries, bool isIncrementalRetry)
        {
            this.defaultRetryIntervalMs = retryIntervalMs;
            this.maxRetries = maxRetries;
            this.isIncrementalRetry = isIncrementalRetry;
            this.RetryCount = 0;

            this.currentWaitTimeMs = 0;
            this.nextWaitTimeMs = this.defaultRetryIntervalMs;
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
                        Trace.TraceError($"[RetryManager] Execution faild with {this.RetryCount} retries.");
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

            this.currentWaitTimeMs = this.nextWaitTimeMs;
            this.nextWaitTimeMs = this.GetNextWaitTime();

            Interlocked.Increment(ref this.retryCount);

            Debug.Assert(this.currentWaitTimeMs >= 0);
            await Task.Delay(this.currentWaitTimeMs);
        }

        public int GetNextWaitTime()
        {
            if (!this.isIncrementalRetry)
            {
                return this.defaultRetryIntervalMs;
            }
            else
            {
                return this.currentWaitTimeMs * this.waitIncrementalFactor;
            }
        }

        public void Reset()
        {
            this.RetryCount = 0;
            this.currentWaitTimeMs = 0;
            this.nextWaitTimeMs = this.defaultRetryIntervalMs;
        }
    }
}
