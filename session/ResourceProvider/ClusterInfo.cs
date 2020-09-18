// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ResourceProvider
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    public class ClusterInfo
    {
        public string ClusterName { get; set; }
        public string ClusterId { get; set; }
        public string Capacity { get; set; }
        public string NetworkTopology { get; set; }
        public string AzureStorageConnectionString { get; set; }
    }
}