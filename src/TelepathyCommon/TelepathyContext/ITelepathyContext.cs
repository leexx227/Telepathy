// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Common.TelepathyContext
{
    using System.Threading;

    using Microsoft.Telepathy.Common.Registry;

    public interface ITelepathyContext
    {
        CancellationToken CancellationToken { get; }

        IClusterContext ClusterContext { get; }

        IRegistry Registry { get; }
    }
}