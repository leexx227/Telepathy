// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System.Collections.Generic;
using Microsoft.Telepathy.Common;

namespace Microsoft.Telepathy.ResourceProvider.Impls.AzureBatch
{
    using System;
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Auth;

    public static class AzureBatchConfiguration
    {
        public static string AzureBatchServiceUrl { get; set; }

        public static string AzureBatchAccountName { get; set; }

        public static string AzureBatchAccountKey { get; set; }

        public static string AzureBatchPoolName { get; set; }

        public static string BatchJobId { get; set; }

        public static string SoaStorageConnectionString { get; set; }

        public static string GetBatchJobId()
        {
            if (!string.IsNullOrEmpty(BatchJobId))
            {
                return BatchJobId;
            }

            return AzureBatchEnvVarReader.GetJobId();
        }

        public static BatchClient GetBatchClient()
        {
            if (string.IsNullOrEmpty(AzureBatchServiceUrl))
            {
                throw new InvalidOperationException($"{nameof(AzureBatchServiceUrl)} is not properly set.");
            }

            if (string.IsNullOrEmpty(AzureBatchAccountName))
            {
                throw new InvalidOperationException($"{nameof(AzureBatchAccountName)} is not properly set.");
            }

            if (string.IsNullOrEmpty(AzureBatchAccountKey))
            {
                throw new InvalidOperationException($"{nameof(AzureBatchAccountKey)} is not properly set.");
            }

            return BatchClient.Open(new BatchSharedKeyCredentials(AzureBatchServiceUrl, AzureBatchAccountName, AzureBatchAccountKey));
        }

        public static void InitializeAzureBatchConfiguration(AzureBatchSessionConfiguration configuration)
        {
            AzureBatchServiceUrl = configuration.AzureBatchServiceUrl;
            AzureBatchAccountName = configuration.AzureBatchAccountName;
            AzureBatchAccountKey = configuration.AzureBatchAccountKey;
            SoaStorageConnectionString = configuration.SoaStorageConnectionString;
            AzureBatchPoolName = configuration.AzureBatchPoolName;
        }
    }
}