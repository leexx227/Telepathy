// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Common
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class AzureBatchSessionConfiguration
    {
        public string AzureBatchServiceUrl { get; set; }
        public string AzureBatchAccountName { get; set; }
        public string AzureBatchAccountKey { get; set; }
        public string AzureBatchPoolName { get; set; }
        public string SoaStorageConnectionString { get; set; }
        public string RedisConnectionKey { get; set; }
    }
}
