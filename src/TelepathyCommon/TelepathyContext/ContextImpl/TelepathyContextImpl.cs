// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Common.TelepathyContext.ContextImpl
{
    using System.Threading;

    using Microsoft.Telepathy.Common.Registry;

    // TODO: Remove me
    public class SoaContext : ITelepathyContext
    {
        public static readonly ITelepathyContext Default = new SoaContext();

        public SoaContext()
        {
            this.ClusterContext = SoaFabricContext.Default;
        }

        public SoaContext(string connectionString)
        {
            this.ClusterContext = new SoaFabricContext(connectionString);
        }

        public SoaContext(EndpointsConnectionString connectionString)
        {
            this.ClusterContext = new SoaFabricContext(connectionString);
        }

        public CancellationToken CancellationToken { get; }

        public IRegistry Registry { get; } = new NonHARegistry();

        public IClusterContext ClusterContext { get; }
    }
}