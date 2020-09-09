// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Microsoft.Telepathy.Common;
using StackExchange.Redis;

namespace Microsoft.Telepathy.QueueManager.NsqMonitor
{
    using Microsoft.Telepathy.Session;
    using NsqSharp.Api;
    using System;
    using System.Collections.Generic;
    using System.Data.SqlTypes;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class NsqMonitor
    {
        private readonly string _sessionId;

        private readonly string _batchId;

        private readonly string _batchQueueId;

        private const int PullQueueMinGap = 1000;

        private const int PullQueueMaxGap = 10000;

        private int _pullQueueGap = PullQueueMinGap;

        private int _clientTimeout;

        public event EventHandler Exit;

        public Action<string, BatchClientState, bool> ReportBatchClientStateAction;

        private List<NsqdHttpClient> _nsqdHttpClients;

        private IDatabase _cache;

        public NsqMonitor(string sessionId, string batchId, int clientTimeout, Action<string, BatchClientState, bool> reportBatchClientStateAction)
        {
            this._sessionId = sessionId;
            this._batchId = batchId;
            this._clientTimeout = clientTimeout;
            this.ReportBatchClientStateAction = reportBatchClientStateAction;
            this._batchQueueId = SessionConfigurationManager.GetBatchClientQueueId(sessionId, batchId);
            this._nsqdHttpClients = NsqManager.GetAllNsqdClients(_batchQueueId);
            _cache = RedisDB.Connection.GetDatabase();
        }

        public async Task StartAsync()
        {
            try
            {
                await this.QueryQueueChangeAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private async Task QueryQueueChangeAsync()
        {
            Console.WriteLine($"[NsqMonitor] Start to monitor batch queue {_batchQueueId}");
            BatchClientState previousClientState = BatchClientState.Initialized;
            BatchClientState currentClientState = previousClientState;
            DateTime lastChangeTime = DateTime.Now;
            bool shouldExit = false;
            while (true)
            {
                if (shouldExit)
                {
                    break;
                }

                //Check from redis to get current client state if set as EndOfRequest or EndOfResponse or Exited
                var hashKey = SessionConfigurationManager.GetRedisBatchClientStateKey(_sessionId);
                var storedClientState = _cache.HashGet(hashKey, _batchId).ToString();
                if (string.Equals(storedClientState, BatchClientState.EndOfRequest.ToString()) ||
                    string.Equals(storedClientState, BatchClientState.EndOfResponse.ToString()) || string.Equals(storedClientState, BatchClientState.Exited.ToString()))
                {
                    shouldExit = true;
                    ReportBatchClientStateAction(_batchId, (BatchClientState)Enum.Parse(typeof(BatchClientState),storedClientState), shouldExit);
                    break;
                }

                try
                {
                    int requestNumber = NsqManager.GetRequestNumber(this._nsqdHttpClients, this._batchQueueId);
                    Console.WriteLine($"[NsqMonitor] {_batchQueueId} => Current queue requests number is {requestNumber}");
                    if (requestNumber > 0)
                    {
                        lastChangeTime = DateTime.Now;
                        if (previousClientState == BatchClientState.Initialized)
                        {
                            currentClientState = BatchClientState.Active;
                            ReportBatchClientStateAction(_batchId, currentClientState, shouldExit);
                        }
                    }
                    else
                    {
                        //client is timeout
                        DateTime currentTime = DateTime.Now;
                        if ((currentTime - lastChangeTime) >= TimeSpan.FromMilliseconds(_clientTimeout))
                        {
                            currentClientState = BatchClientState.Timeout;
                            shouldExit = true;
                            ReportBatchClientStateAction(_batchId, currentClientState, shouldExit);
                        }
                    }
                    Console.WriteLine($"[NsqMonitor] {_batchQueueId} => PreviousClientState is {previousClientState}");
                    Console.WriteLine($"[NspMonitor] {_batchQueueId} => CurrentClientState is {currentClientState}");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }

                if (!shouldExit)
                {
                    //Sleep and pull request from queue
                    await Task.Delay(this._pullQueueGap);
                    if (this._pullQueueGap < PullQueueMaxGap)
                    {
                        this._pullQueueGap *= 2;
                        if (this._pullQueueGap > PullQueueMaxGap)
                        {
                            this._pullQueueGap = PullQueueMaxGap;
                        }
                    }
                }
            }
        }
    }
}
