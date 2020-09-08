// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ResourceProvider
{
    using Session;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ISchedulerAdapter
    {
        Task<bool> UpdateSessionInfoAsync(string sessionId, Dictionary<string, object> properties);

        Task<bool> FinishTaskAsync(string sessionId, string taskUniqueId);

        Task<bool> ExcludeNodeAsync(string sessionId, string nodeName);

        Task RequeueOrFailSessionAsync(string sessionId, string reason);

        Task FailSessionAsync(string sessionId, string reason);

        Task FinishSessionAsync(string sessionId, string reason);

        Task<(SessionState jobState, int autoMax, int autoMin)> RegisterJobAsync(string sessionId);

        Task<int?> GetTaskErrorCode(string sessionId, string globalTaskId);
    }
}
