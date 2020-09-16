// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Microsoft.Extensions.Primitives;
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

        private readonly int _clientTimeout;

        public event EventHandler Exit;

        public Action<string, BatchClientState, bool> ReportBatchClientStateAction;

        private List<NsqdHttpClient> _nsqdHttpClients;

        private IDatabase _cache;

        private int _totalTaskNumber = 0;

        private int _historyTaskNumber = 0;

        public NsqMonitor(string sessionId, string batchId, int clientTimeout, Action<string, BatchClientState, bool> reportBatchClientStateAction)
        {
            this._sessionId = sessionId;
            this._batchId = batchId;
            this._clientTimeout = clientTimeout;
            this.ReportBatchClientStateAction = reportBatchClientStateAction;
            this._batchQueueId = SessionConfigurationManager.GetBatchClientQueueId(sessionId, batchId);
            this._nsqdHttpClients = NsqManager.GetAllNsqdClients(_batchQueueId, _clientTimeout);
            _cache = RedisDB.Connection.GetDatabase();
        }

        public async Task StartAsync()
        {
            try
            {
                if (_nsqdHttpClients.Count == 0)
                {
                    var hashKey = SessionConfigurationManager.GetRedisBatchClientStateKey(_sessionId);
                    HashEntry[] clientEntry = { new HashEntry(_batchId, BatchClientState.Timeout.ToString()) };
                    _cache.HashSet(hashKey, clientEntry);
                    ReportBatchClientStateAction?.Invoke(_batchId, BatchClientState.Timeout, true);
                    return;
                }

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
            BatchClientState previousClientState = BatchClientState.Active;
            BatchClientState currentClientState = previousClientState;
            DateTime lastChangeTime = DateTime.Now;
            bool shouldExit = false;
            while (true)
            {
                if (shouldExit)
                {
                    break;
                }
                try
                {
                    //Check from redis to get current client state if set as EndOfRequest or EndOfResponse or Exited
                    var hashKey = SessionConfigurationManager.GetRedisBatchClientStateKey(_sessionId);
                    var storedClientState = _cache.HashGet(hashKey, _batchId).ToString();
                    if (BatchClient.IsEndState(Enum.Parse<BatchClientState>(storedClientState)))
                    {
                        shouldExit = true;
                        ReportBatchClientStateAction(_batchId, (BatchClientState)Enum.Parse(typeof(BatchClientState), storedClientState), shouldExit);
                        break;
                    }
                    if (string.Equals(storedClientState, BatchClientState.EndOfRequest.ToString()))
                    {
                        _totalTaskNumber = _totalTaskNumber == 0 ? (int)_cache.StringGet(SessionConfigurationManager.GetRedisBatchClientTotalTasksKey(_sessionId, _batchId)) : _totalTaskNumber;
                        //Check the response number to determine current batch client if EndOfResponse
                        var finishTasksKey =
                            SessionConfigurationManager.GetRedisBatchClientFinishTasksKey(_sessionId, _batchId);
                        var finishTasksNum = _cache.SetLength(finishTasksKey);
                        if (_totalTaskNumber == finishTasksNum)
                        {
                            currentClientState = BatchClientState.EndOfResponse;
                            HashEntry[] clientEntry = { new HashEntry(_batchId, currentClientState.ToString()) };
                            _cache.HashSet(hashKey, clientEntry);
                            shouldExit = true;
                        }

                    }
                    else if (string.Equals(storedClientState, BatchClientState.Active.ToString()))
                    {
                        int currentTaskNumber = NsqManager.GetRequestNumber(this._nsqdHttpClients, this._batchQueueId);
                        Console.WriteLine($"[NsqMonitor] {_batchQueueId} => Current queue requests number is {currentTaskNumber}");
                        if (currentTaskNumber - _historyTaskNumber > 0)
                        {
                            lastChangeTime = DateTime.Now;
                            currentClientState = BatchClientState.Active;
                            _historyTaskNumber = currentTaskNumber;
                            ReportBatchClientStateAction(_batchId, currentClientState, shouldExit);
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
