// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Common.TelepathyContext.ContextImpl
{
    using Microsoft.Telepathy.Common.Registry;
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;
    using Microsoft.Azure.Management.ContainerService.Fluent;
    using Microsoft.Azure.Management.Fluent;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using System.IO;

    public class KubeContext : IClusterContext
    {
        public static readonly IClusterContext Default = new KubeContext();

        public EndpointsConnectionString ConnectionString { set;  get; }

        public IRegistry Registry => new ContainerRedisRegistry();

        public static Lazy<IKubernetes> ClusterClient => new Lazy<IKubernetes>(() =>
        {
            // TODO: should be get authentication info when deploy 
            var clientId = "";
            var clientSecret = "";
            var tenantId = "";
            var subscriptionId = "";
            var resourceGroupName = "";
            var clusterName = "";
            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId,
                    clientSecret,
                    tenantId,
                    AzureEnvironment.AzureGlobalCloud);

            var azure = Azure.Configure().Authenticate(credentials).WithSubscription(subscriptionId);
            IKubernetesCluster kubernetesCluster = azure.KubernetesClusters.GetByResourceGroup(resourceGroupName, clusterName);

            var buffer = kubernetesCluster.UserKubeConfigContent;
            MemoryStream stream = new MemoryStream(buffer);
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(stream, null, null);
            IKubernetes client = new Kubernetes(config);
            return client;

        });

        public Task<string> ResolveSingletonServicePrimaryAsync(string sessionId, CancellationToken token)
        {
            // Get session launcher address via sessionId from Redis
            string sessionLauncherName = Registry.GetValueAsync<string>(null, sessionId, new CancellationToken()).GetAwaiter().GetResult();
            return Task.FromResult<string>(ClusterClient.Value.ReadNamespacedPodAsync(sessionLauncherName, "telepathy").Result.Status.PodIP);
        }

        public Task<T> GetClusterClient<T>()
        {
            if (typeof(T) == typeof(IKubernetes))
            {
                return Task.FromResult((T)ClusterClient.Value);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
