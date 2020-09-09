// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Microsoft.Telepathy.Common;

namespace Microsoft.Telepathy.QueueManager.NsqMonitor
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Collections.Concurrent;
    using System.Text;

    public class NsqDelegation
    {
        private ConcurrentDictionary<string, NsqMonitorEntry> QueueMonitors = new ConcurrentDictionary<string, NsqMonitorEntry>();

        public async Task RegisterBatchClient(string sessionId, string batchId, int clientTimeout)
        {
            Console.WriteLine($"[NsqDelegation] Start register batch client {batchId} for session {sessionId}.");
            var batchQueueId = SessionConfigurationManager.GetBatchClientQueueId(sessionId, batchId);
            if (!QueueMonitors.TryGetValue(batchId, out NsqMonitorEntry queueMonitorEntry))
            {
                queueMonitorEntry = new NsqMonitorEntry(sessionId, batchId, clientTimeout);
                queueMonitorEntry.Exit += new EventHandler(this.QueueMonitorEntry_Exit);
            }

            this.QueueMonitors[batchQueueId] = queueMonitorEntry;

            await queueMonitorEntry.StartAsync();
        }

        private void QueueMonitorEntry_Exit(object sender, EventArgs e)
        {
            NsqMonitorEntry entry = (NsqMonitorEntry)sender;
            this.QueueMonitors.Remove(entry.BatchQueueId, out NsqMonitorEntry result);
            entry.Exit -= new EventHandler(this.QueueMonitorEntry_Exit);
            Console.WriteLine($"[NsqDelegation] {entry.BatchQueueId} End: NsqMonitorEntry Exit, exit state is {entry.CurrentQueueState}.");
        }

    }
}
