// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System.Reflection.PortableExecutable;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Telepathy.Session.ServiceRegistration;
using Microsoft.Telepathy.Session.Util;

namespace Microsoft.Telepathy.ResourceProvider.Impls.AzureBatch
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using SessionLauncher;

    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.Azure.Storage;
    using Microsoft.Telepathy.Session;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;

    public class AzureBatchResourceProvider : ResourceProvider
    {
        private CloudStorageAccount cloudStorageAccount = CloudStorageAccount.Parse(ResourceProviderRuntimeConfiguration.SessionLauncherStorageConnectionString);

        private const string RuntimeContainer = "runtime";

        private const string ServiceContainer = "service-assembly";

        private const string RuntimeFolder = "hostagent";

        private const string RuntimeExe = "Microsoft.Telepathy.HostAgent.Launcher.exe";

        private const string AzureBatchPrepJobWorkingDir = "%AZ_BATCH_JOB_PREP_WORKING_DIR%";

        private static readonly string JobPrepCmdLine = @"cmd /c """;

        private static readonly string JobReleaseCmdLine = $@"cmd /c rd /s /q {AzureBatchPrepJobWorkingDir}";


        // TODO: remove parameter less ctor and add specific parameters for the sake of test-ablity
        public AzureBatchResourceProvider()
        {
            this.clusterInfo = new ClusterInfo
            {
                ClusterName = AzureBatchConfiguration.AzureBatchPoolName,
                NetworkTopology = "Public",
                AzureStorageConnectionString =
                AzureBatchConfiguration.SoaStorageConnectionString
            };
        }


        public override async Task TerminateAsync(string sessionId)
        {
            using (var batchClient = AzureBatchConfiguration.GetBatchClient())
            {
                try
                {
                    var batchJob = await batchClient.JobOperations.GetJobAsync(AzureBatchSessionJobIdConverter.ConvertToAzureBatchJobId(sessionId));
                    //Conflict error occurs when terminate a job which is not in the state of Enabling or Active
                    if (batchJob.State == Azure.Batch.Common.JobState.Active || batchJob.State == Azure.Batch.Common.JobState.Enabling || batchJob.State == Azure.Batch.Common.JobState.Disabling)
                    {
                        await batchJob.TerminateAsync();
                    }
                }
                catch (BatchException e)
                {
                    if (e.RequestInformation != null && e.RequestInformation.HttpStatusCode != null)
                    {
                        if (e.RequestInformation.HttpStatusCode == HttpStatusCode.NotFound)
                        {
                            //TraceHelper.TraceEvent(sessionId, TraceEventType.Warning, "[AzureBatchSessionLauncher] .TerminateAsync: The specified job can't be found, maybe has deleted.");
                            return;
                        }
                    }
                    //TraceHelper.TraceEvent(sessionId, TraceEventType.Error, "[AzureBatchSessionLauncher] .TerminateAsync: Job was failed to terminate : {0}.", e);
                }
                catch (Exception e)
                {
                    //TraceHelper.TraceEvent(sessionId, TraceEventType.Error, "[AzureBatchSessionLauncher] .TerminateAsync: Job was failed to terminate : {0}.", e);
                }

            }
        }

        public string GetLocalIPv4(NetworkInterfaceType _type)
        {
            string output = "";
            foreach (NetworkInterface item in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (item.NetworkInterfaceType == _type && item.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in item.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            output = ip.Address.ToString();
                        }
                    }
                }
            }
            return output;
        }


        protected override async Task<ResourceAllocateInfo> CreateAndSubmitSessionJob(
            SessionInitInfo initInfo,
            string callId,
            ServiceRegistration registration,
            ResourceAllocateInfo sessionAllocateInfo,
            string serviceName)
        {
            try
            {
                using (var batchClient = AzureBatchConfiguration.GetBatchClient())
                {
                    var pool = await batchClient.PoolOperations.GetPoolAsync(AzureBatchConfiguration.AzureBatchPoolName);
                    ODATADetailLevel detailLevel = new ODATADetailLevel
                    {
                        SelectClause = "affinityId, ipAddress"
                    };
                    var nodes = await pool.ListComputeNodes(detailLevel).ToListAsync();
                    if (nodes.Count < 1)
                    {
                        throw new InvalidOperationException("Compute node count in selected pool is less than 1.");
                    }

                    sessionAllocateInfo.Id = string.Empty;

                    IList<EnvironmentSetting> ConstructEnvironmentVariable()
                    {
                        List<EnvironmentSetting> env = new List<EnvironmentSetting>(); // Can change to set to ensure no unintended overwrite
                        if (registration.EnvironmentVariables != null)
                        {
                            foreach (var entry in registration.EnvironmentVariables)
                            {
                                env.Add(new EnvironmentSetting(entry.Key, entry.Value));
                            }
                        }     

                        //TODO: get dispatcherIP from AKS API
                        env.Add(new EnvironmentSetting(SessionConstants.ServiceTimeoutEnvVar, initInfo.ServiceTimeout > 0 ? initInfo.ServiceTimeout.ToString() : registration.ServiceTimeout.ToString()));
                        //env.Add(new EnvironmentSetting(SessionConstants.DispatcherIPEnvVar, GetLocalIPv4(NetworkInterfaceType.Ethernet)));
                        env.Add(new EnvironmentSetting(SessionConstants.DispatcherIPEnvVar, "10.94.201.221"));
                        env.Add(new EnvironmentSetting(SessionConstants.ServiceConcurrencyEnvVar, registration.ServiceConcurrency.ToString()));
                        env.Add(new EnvironmentSetting(SessionConstants.PrefetchCountEnvVar, initInfo.PrefetchCount > 0 ? initInfo.PrefetchCount.ToString() : registration.PrefetchCount.ToString()));
                        env.Add(new EnvironmentSetting(SessionConstants.ServiceFullPathEnvVar, registration.ServiceFullPath));
                        env.Add(new EnvironmentSetting(SessionConstants.ServiceInitializeTimeoutEnvVar, registration.ServiceInitializeTimeout.ToString()));
                        env.Add(new EnvironmentSetting(SessionConstants.SessionIDEnvVar, sessionAllocateInfo.Id));
                        env.Add(new EnvironmentSetting(SessionConstants.ServiceLanguageEnvVar, registration.ServiceLanguage));

                        if (initInfo.Environments != null)
                        {
                            foreach (KeyValuePair<string, string> entry in initInfo.Environments)
                            {
                                env.Add(new EnvironmentSetting(entry.Key, entry.Value));
                            }
                        }
                        //Establish a link via ev between TELEPATHY_SERVICE_WORKING_DIR and AZ_BATCH_JOB_PREP_WORKING_DIR
                        env.Add(new EnvironmentSetting(SessionConstants.TelepathyWorkingDirEnvVar, AzureBatchPrepJobWorkingDir));
                        return env;
                    }

                    ResourceFile GetResourceFileReference(string containerName, string blobPrefix)
                    {
                        var sasToken = AzureStorageUtil.ConstructContainerSas(this.cloudStorageAccount, containerName, SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Read);
                        ResourceFile rf;
                        if (string.IsNullOrEmpty(blobPrefix))
                        {
                            rf = ResourceFile.FromStorageContainerUrl(sasToken);
                        }
                        else
                        {
                            rf = ResourceFile.FromStorageContainerUrl(sasToken, blobPrefix: blobPrefix);
                        }

                        return rf;
                    }

                    async Task<string> CreateJobAsync()
                    {
                        //TODO: need a function to test if all parameters are legal.
                        if (initInfo.MaxServiceInstance <= 0)
                        {
                            throw new ArgumentException("MaxServiceNumber value is invalid.");
                        }
                        string newJobId = AzureBatchSessionJobIdConverter.ConvertToAzureBatchJobId(AzureBatchSessionIdGenerator.GenerateSessionId());
                        var job = batchClient.JobOperations.CreateJob(newJobId, new PoolInformation() { PoolId = AzureBatchConfiguration.AzureBatchPoolName });
                        job.JobPreparationTask = new JobPreparationTask(JobPrepCmdLine);
                        job.JobPreparationTask.UserIdentity = new UserIdentity(new AutoUserSpecification(elevationLevel: ElevationLevel.Admin, scope: AutoUserScope.Task));
                        job.JobPreparationTask.ResourceFiles = new List<ResourceFile>()
                        {
                            GetResourceFileReference(RuntimeContainer, RuntimeFolder),
                            GetResourceFileReference(ServiceContainer,initInfo.ServiceName.ToLower())
                        };

                        job.JobReleaseTask = new JobReleaseTask(JobReleaseCmdLine);
                        job.JobReleaseTask.UserIdentity = new UserIdentity(new AutoUserSpecification(elevationLevel: ElevationLevel.Admin, scope: AutoUserScope.Task));

                        // Set Meta Data
                        if (job.Metadata == null)
                        {
                            job.Metadata = new List<MetadataItem>();
                        }

                        Dictionary<string, string> jobMetadata = new Dictionary<string, string>();
                                                                    
                        if (initInfo.ServiceVersion != null)
                        {
                            jobMetadata.Add(SessionSettingsConstants.ServiceVersion, initInfo.ServiceVersion?.ToString());
                        }

                        if (initInfo.MaxServiceInstance > 0)
                        {
                            jobMetadata.Add(SessionSettingsConstants.ServiceInstanceNumber, initInfo.MaxServiceInstance.ToString());
                        }

                        Dictionary<string, int?> jobOptionalMetadata = new Dictionary<string, int?>()
                                                                           {
                                                                               { SessionSettingsConstants.ClientIdleTimeout, initInfo.ClientIdleTimeout },
                                                                               { SessionSettingsConstants.SessionIdleTimeout, initInfo.SessionIdleTimeout }
                                                                           };

                        job.Metadata = job.Metadata.Concat(jobMetadata.Select(p => new MetadataItem(p.Key, p.Value)))
                            .Concat(jobOptionalMetadata.Where(p => p.Value.HasValue).Select(p => new MetadataItem(p.Key, p.Value.ToString()))).ToList();

                        job.DisplayName = $"{job.Id} - {initInfo.ServiceName}";
                        await job.CommitAsync();
                        return job.Id;
                    }

                    var jobId = await CreateJobAsync();
                    string sessionId = AzureBatchSessionJobIdConverter.ConvertToSessionId(jobId);
                    if (!sessionId.Equals("-1"))
                    {
                        sessionAllocateInfo.Id = sessionId;
                    }
                    else
                    {
                        //TraceHelper.TraceEvent(TraceEventType.Error, "[AzureBatchSessionLauncher] .CreateAndSubmitSessionJob: JobId was failed to parse. callId={0}, jobId={1}.", callId, jobId);
                    }
                    //ConstructEnvironmentVariable use sessionAllocationInfo.id to set session id in ABS task environment
                    var environment = ConstructEnvironmentVariable();

                    Task AddTasksAsync()
                    {
                        int numTasks = initInfo.MaxServiceInstance >= 0 ? initInfo.MaxServiceInstance : nodes.Count;

                        var comparer = new EnvironmentSettingComparer();

                        CloudTask CreateTask(string taskId)
                        {
                            CloudTask cloudTask = new CloudTask(taskId, $@"cmd /c ""%{ SessionConstants.TelepathyWorkingDirEnvVar}%\{RuntimeFolder}\{RuntimeExe}"" ");
                            cloudTask.UserIdentity = new UserIdentity(new AutoUserSpecification(elevationLevel: ElevationLevel.Admin, scope: AutoUserScope.Pool));
                            cloudTask.EnvironmentSettings = cloudTask.EnvironmentSettings == null ? environment : environment.Union(cloudTask.EnvironmentSettings, comparer).ToList();
                            return cloudTask;
                        }

                        //TODO: task id type should be changed from int to string
                        var tasks = Enumerable.Range(0, numTasks - 1).Select(_ => CreateTask(Guid.NewGuid().ToString())).ToArray();
                        tasks = tasks.Union(new[] { CreateTask(Guid.NewGuid().ToString()) }).ToArray();
                       
                        return batchClient.JobOperations.AddTaskAsync(jobId, tasks);
                    }

                    await AddTasksAsync();
                    return sessionAllocateInfo;
                }
            }
            catch (Exception ex)
            {
                //TraceHelper.TraceEvent(TraceEventType.Error, $"[{nameof(AzureBatchResourceProvider)}] .{nameof(this.CreateAndSubmitSessionJob)}: Exception happens: {ex.ToString()}");
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        private class EnvironmentSettingComparer : IEqualityComparer<EnvironmentSetting>
        {
            public bool Equals(EnvironmentSetting x, EnvironmentSetting y)
            {
                if (x == null || y == null)
                {
                    return false;
                }

                return x.Name == y.Name;
            }

            public int GetHashCode(EnvironmentSetting obj)
            {
                return obj.Name.GetHashCode();
            }
        }


        public Task<ResourceAllocateInfo> GrowSessionResourceAsync(Session session)
        {
            throw new NotImplementedException();
        }

        public Task<ResourceAllocateInfo> ShrinkSessionResourceAsync(Session session)
        {
            throw new NotImplementedException();
        }

        public Task<ClusterInfo> GetClusterInfoAsync()
        {
            throw new NotImplementedException();
        }

        protected internal override ServiceRegistrationRepo CreateServiceRegistrationRepo(string regPath) =>
            new ServiceRegistrationRepo(
                regPath,
                new AzureBlobServiceRegistrationStore(ResourceProviderRuntimeConfiguration.SessionLauncherStorageConnectionString),
                Environment.GetEnvironmentVariable(SessionConstants.TelepathyWorkingDirEnvVar, EnvironmentVariableTarget.Machine));
    }
}