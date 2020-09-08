﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Microsoft.Telepathy.Common;
using StackExchange.Redis;

namespace Microsoft.Telepathy.Session
{
    using System;
    using System.Collections.Concurrent;

    public class Session
    {
        public string SessionId { set; get; }
        public SessionState State { get; }
        public SessionInitInfo SessionInitInfo { get; }

        private readonly IDatabase _cache;

        public Session(SessionInitInfo sessionInitInfo)
        {
            SessionInitInfo = sessionInitInfo;
            State = SessionState.Creating;
            _cache = RedisDB.Connection.GetDatabase();
        }

        public void AddBatchClient(string clientId)
        {
            Console.WriteLine($"Session: {SessionId} add batchclient {clientId} .");
            //Add batch client info in Redis
            var setKey = SessionConfigurationManager.GetRedisBatchClientIdKey(SessionId);
            _cache.SetAdd(setKey, clientId);
            HashEntry[] clientEntry = { new HashEntry(clientId, BatchClientState.Initialized.ToString()) };
            var hashKey = SessionConfigurationManager.GetRedisBatchClientStateKey(SessionId);
            _cache.HashSet(hashKey, clientEntry);
        }

        public void RemoveBatchClient(string clientId)
        {
            var setKey =SessionConfigurationManager.GetRedisBatchClientIdKey(SessionId);
            _cache.SetRemove(setKey, clientId);
            var hashKey = SessionConfigurationManager.GetRedisBatchClientStateKey(SessionId);
            _cache.HashDelete(hashKey, clientId);
        }

        public bool UpdateBatchClientState(string clientId, BatchClientState state)
        {
            //TODO: ErrorHandling, how the state machine work? Check previous state first.
            var hashKey = SessionConfigurationManager.GetRedisBatchClientStateKey(SessionId);
            var storedState = (BatchClientState)Enum.Parse(typeof(BatchClientState),_cache.HashGet(hashKey, clientId).ToString());
            //Only previous state is not an end state, the update operation can execute
            if (storedState == BatchClientState.Timeout || storedState == BatchClientState.Exited)
            {
                return false;
            }

            HashEntry[] clientEntry = { new HashEntry(clientId, state.ToString()) };
            _cache.HashSet(hashKey, clientEntry);
            return true;
        }

        public bool UpdateSessionState(string sessionId, SessionState state)
        {
            var sessionStateKey = SessionConfigurationManager.GetRedisSessionStateKey(sessionId);
            if (!_cache.KeyExists(sessionStateKey))
            {
                _cache.StringSet(sessionStateKey, state.ToString());
                return true;
            }
            var storedState = (SessionState) Enum.Parse(typeof(SessionState), _cache.StringGet(sessionStateKey).ToString());
            switch (storedState)
            {
                case SessionState.Canceling:
                case SessionState.Canceled:
                case SessionState.Completed:
                case SessionState.Failed:
                case SessionState.Finishing:
                case SessionState.Finished:
                    return false;
            }

            _cache.StringSet(sessionStateKey, state.ToString());
            return true;
        }
    }
}
