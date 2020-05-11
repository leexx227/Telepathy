// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Common.TelepathyContext.ContextImpl
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using Microsoft.Telepathy.Common.Registry;

    // TODO: remove me
    public class SoaFabricContext : IClusterContext
    {
        private const string LocalHost = "localhost";

        public static readonly IClusterContext Default = new SoaFabricContext();

        public SoaFabricContext(string connectionString)
            : this(EndpointsConnectionString.ParseConnectionString(connectionString))
        {
        }

        public SoaFabricContext()
        {
            this.ConnectionString = EndpointsConnectionString.ParseConnectionString(LocalHost);
        }

        public SoaFabricContext(EndpointsConnectionString connectionString)
        {
            this.ConnectionString = connectionString;
        }

        public EndpointsConnectionString ConnectionString { get; }

        public IRegistry Registry => throw new NotImplementedException();

        public async Task<string> ResolveSingletonServicePrimaryAsync(string serviceName, CancellationToken token)
        {
            return this.ConnectionString.ConnectionString;
        }

        public Task<T> GetClusterClient<T>()
        {
            throw new NotImplementedException();
        }
    }
}