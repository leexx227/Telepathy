﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Session.ServiceRegistration
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    using Microsoft.Azure.Storage;
    using Microsoft.Azure.Storage.Blob;

    // TODO: azure storage client side retry
    // TODO: TEST CASE 1 - mixed cases in service name
    public class AzureBlobServiceRegistrationStore : IServiceRegistrationStore
    {
        private const string ServiceRegistrationBlobContainerName = "service-registry";

        private readonly CloudBlobContainer _blobContainer;

        private Lazy<MD5> md5 = new Lazy<MD5>(() => MD5.Create());

        public AzureBlobServiceRegistrationStore(string connectionString)
        {
            this._blobContainer = CloudStorageAccount.Parse(connectionString).CreateCloudBlobClient().GetContainerReference(ServiceRegistrationBlobContainerName);
            this._blobContainer.CreateIfNotExists();
        }

        public async Task DeleteAsync(string serviceName, Version serviceVersion)
        {
            var blob = this.GetServiceRegistrationBlockBlobReference(serviceName, serviceVersion);
            await blob.DeleteIfExistsAsync();
        }

        // TODO: continuously query
        public async Task<List<string>> EnumerateAsync()
        {
            return (await this._blobContainer.ListBlobsSegmentedAsync(null, null)).Results.Select(b => Path.GetFileNameWithoutExtension(b.Uri.ToString())).ToList();
        }

        public string CalculateMd5Hash(byte[] blobData)
        {
            var hash = md5.Value.ComputeHash(blobData);
            return Convert.ToBase64String(hash);
        }

        // Skip to download again only when the cloud blob has no changes
        public async Task<string> ExportToTempFileAsync(string serviceName, Version serviceVersion)
        {
            var blob = this.GetServiceRegistrationBlockBlobReference(serviceName, serviceVersion);
            var filePath = SoaRegistrationAuxModule.GetServiceRegistrationTempFilePath(Path.GetFileNameWithoutExtension(blob.Uri.ToString()));
            Trace.TraceInformation($"Will write Service Registration file to {filePath}");
            // check if the exsiting file's md5 and cloud blob's md5 have the same value 
            if (File.Exists(filePath))
            {
                string fileMd5 = await GetMd5AuxAsync(blob);
                string existingFileMd5 = CalculateMd5Hash(File.ReadAllBytes(filePath));
                if (string.Equals(fileMd5, existingFileMd5))
                {
                    return filePath;
                }
            }

            if (await blob.ExistsAsync())
            {
                await blob.DownloadToFileAsync(filePath, FileMode.Create);
                return filePath;
            }

            return string.Empty;
        }

        public async Task<string> GetAsync(string serviceName, Version serviceVersion)
        {
            var blob = this.GetServiceRegistrationBlockBlobReference(serviceName, serviceVersion);
            if (await blob.ExistsAsync())
            {
                return await blob.DownloadTextAsync();
            }

            return string.Empty;
        }

        private async Task<string> GetMd5AuxAsync(CloudBlockBlob blob)
        {
            if (await blob.ExistsAsync())
            {
                await blob.FetchAttributesAsync();
                return blob.Properties.ContentMD5;
            }

            return string.Empty;
        }

        public async Task<string> GetMd5Async(string serviceName, Version serviceVersion)
        {
            var blob = this.GetServiceRegistrationBlockBlobReference(serviceName, serviceVersion);
            return await GetMd5AuxAsync(blob);
        }

        public async Task ImportFromFileAsync(string filePath, string serviceName)
        {
            Debug.Assert(filePath != null);
            if (!File.Exists(filePath))
            {
                throw new InvalidOperationException($"File {filePath} for uploading does not exist.");
            }

            string svcName;
            if (string.IsNullOrEmpty(serviceName))
            {
                svcName = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            }
            else
            {
                svcName = SoaRegistrationAuxModule.GetRegistrationFileName(serviceName, null);
            }

            var blob = this.GetServiceRegistrationBlockBlobReference(svcName, null);
            await blob.UploadFromFileAsync(filePath);
        }

        public async Task SetAsync(string serviceName, Version serviceVersion, string serviceRegistration)
        {
            var blob = this.GetServiceRegistrationBlockBlobReference(serviceName, serviceVersion);
            await blob.UploadTextAsync(serviceRegistration);
        }

        private CloudBlockBlob GetServiceRegistrationBlockBlobReference(string serviceName, Version serviceVersion)
        {
            var fileName = SoaRegistrationAuxModule.GetRegistrationFileName(serviceName, serviceVersion);
            Trace.TraceInformation($"Getting block blob reference for {fileName}.");
            return this._blobContainer.GetBlockBlobReference(fileName);
        }
    }
}