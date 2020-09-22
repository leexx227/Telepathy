// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Microsoft.Telepathy.Common;
using StackExchange.Redis;

namespace Microsoft.Telepathy.Session
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public class Session
    {
        public SessionState State { get; }
        public SessionInitInfo SessionInitInfo { get; }

        private static readonly IDatabase Cache = RedisDB.Connection.GetDatabase();

        public Session(SessionInitInfo sessionInitInfo)
        {
            SessionInitInfo = sessionInitInfo;
            State = SessionState.Creating;
        }

        public static void RegisterSessionInfo(string sessionId, SessionInitInfo sessionInitInfo)
        {
            var hashkey = SessionConfigurationManager.GetRedisSessionInitInfoKey(sessionId);
            HashEntry[] redisSessionInitInfoHash =
            {
                new HashEntry("clientTimeout", sessionInitInfo.ClientIdleTimeout),
                new HashEntry("sessionTimeout", sessionInitInfo.SessionIdleTimeout) 
            };
            Cache.HashSet(hashkey, redisSessionInitInfoHash);
        }

        public static int AddBatchClient(string sessionId, string clientId)
        {
            Console.WriteLine($"Session: {sessionId} add batchclient {clientId} .");
            //Add batch client info in Redis
            var setKey = SessionConfigurationManager.GetRedisBatchClientIdKey(sessionId);
            Cache.SetAdd(setKey, clientId);
            HashEntry[] clientEntry = { new HashEntry(clientId, BatchClientState.Active.ToString()) };
            var hashKey = SessionConfigurationManager.GetRedisBatchClientStateKey(sessionId);
            Cache.HashSet(hashKey, clientEntry);
            var sessionInitInfoKey = SessionConfigurationManager.GetRedisSessionInitInfoKey(sessionId);
            return (int)Cache.HashGet(sessionInitInfoKey, "clientTimeout");
        }

        public static void RemoveBatchClient(string sessionId, string clientId)
        {
            var setKey =SessionConfigurationManager.GetRedisBatchClientIdKey(sessionId);
            Cache.SetRemove(setKey, clientId);
            var hashKey = SessionConfigurationManager.GetRedisBatchClientStateKey(sessionId);
            Cache.HashDelete(hashKey, clientId);
        }

        public static bool UpdateBatchClientState(string sessionId, string clientId, BatchClientState state, int requestNum = 0)
        {
            //TODO: ErrorHandling, how the state machine work? Check previous state first.
            var hashKey = SessionConfigurationManager.GetRedisBatchClientStateKey(sessionId);
            var storedState = (BatchClientState)Enum.Parse(typeof(BatchClientState), Cache.HashGet(hashKey, clientId).ToString());
            //Only previous state is not an end state, the update operation can execute
            if (BatchClient.IsEndState(storedState))
            {
                return false;
            }

            if (state == BatchClientState.EndOfRequest)
            {
                var tasksKey = SessionConfigurationManager.GetRedisBatchClientTotalTasksKey(sessionId, clientId);
                Cache.StringSet(tasksKey, requestNum.ToString());
                Console.WriteLine($"[Session] UpdateBatchClientState to EndOfRequest, total request number is {requestNum}");
            }

            HashEntry[] clientEntry = { new HashEntry(clientId, state.ToString()) };
            Cache.HashSet(hashKey, clientEntry);
            return true;
        }

        public static bool UpdateSessionState(string sessionId, SessionState state)
        {
            var sessionStateKey = SessionConfigurationManager.GetRedisSessionStateKey(sessionId);
            if (!Cache.KeyExists(sessionStateKey))
            {
                Cache.StringSet(sessionStateKey, state.ToString());
                return true;
            }
            var storedState = (SessionState) Enum.Parse(typeof(SessionState), Cache.StringGet(sessionStateKey).ToString());
            switch (storedState)
            {
                case SessionState.Completed:
                case SessionState.Failed:
                case SessionState.Closing:
                case SessionState.Closed:
                    return false;
            }

            Cache.StringSet(sessionStateKey, state.ToString());
            return true;
        }

        public static bool CloseAllBatchClients(string sessionId)
        {
            Cache.KeyDelete(SessionConfigurationManager.GetRedisSessionInitInfoKey(sessionId));
            //Set all BatchClient state as closed in redis
            var hashkey = SessionConfigurationManager.GetRedisBatchClientStateKey(sessionId);
            var batchClients = Cache.HashGetAll(hashkey);
            List<HashEntry> clients = new List<HashEntry>();
            foreach (var batchClient in batchClients)
            {
                clients.Add(new HashEntry(batchClient.Name, BatchClientState.Closed.ToString()));
            }
            Cache.HashSet(hashkey, clients.ToArray());
            return true;
        }
    }
}
