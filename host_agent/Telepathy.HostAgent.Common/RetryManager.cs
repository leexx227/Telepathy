// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace Microsoft.Telepathy.HostAgent.Common
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

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

        public async Task<TResult> RetryOperationAsync<TResult>(OnAction<TResult> action, OnException onException) =>
            await this.RetryOperationAsync(action, onException, null);

        public async Task<TResult> RetryOperationAsync<TResult>(OnAction<TResult> action, OnException onException,
            Func<bool> isConditionTrue)
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
                        var condition = isConditionTrue == null? true : isConditionTrue();
                        await this.WaitForNextAttempt(condition);
                    }
                    else
                    {
                        Trace.TraceError($"[RetryManager] Execution faild with {this.RetryCount} retries.");
                        throw;
                    }
                }
            }
        }

        public async Task WaitForNextAttempt(bool condition)
        {
            if (!this.HasAttemptsLeft)
            {
                throw new InvalidOperationException("There are no more retry attempts remaining");
            }

            this.currentWaitTimeMs = this.nextWaitTimeMs;
            this.nextWaitTimeMs = this.GetNextWaitTime();

            if (condition)
            {
                Interlocked.Increment(ref this.retryCount);
            }

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
