// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ResourceProvider
{
    using System;
    using System.Threading.Tasks;
    using Session;
    using Session.ServiceRegistration;

    public abstract class ResourceProvider
    {
        protected ClusterInfo clusterInfo;
        public async Task<ResourceAllocateInfo> AllocateSessionResourceAsync(Session session)
        {
            ResourceAllocateInfo resourceAllocateInfo = new ResourceAllocateInfo();
            SessionInitInfo sessionInitInfo = session.SessionInitInfo;
            resourceAllocateInfo.SessionInitInfo = sessionInitInfo;
            string callId = Guid.NewGuid().ToString();
            //TODO:
            // make registration path as registration store token value. Should refactor the way to find registration file later  
            string regPath = SessionConstants.RegistrationStoreToken;
            ServiceRegistrationRepo serviceRegistrationRepo = this.CreateServiceRegistrationRepo(regPath);
            //download service registration file and move to facade folder
            string serviceConfigFile = serviceRegistrationRepo.GetServiceRegistrationPath(sessionInitInfo.ServiceName, sessionInitInfo.ServiceVersion);

            // If the serviceConfigFile wasnt found and serviceversion isnt specified, try getting the service config based on the service's latest version
            if (string.IsNullOrEmpty(serviceConfigFile) && (sessionInitInfo.ServiceVersion == null))
            {
                //TraceHelper.TraceEvent(TraceEventType.Verbose, "[SessionLauncher] .AllocateInternalAsync: Try to find out versioned service.");

                // Get service version in ServiceRegistrationRepo
                Version dynamicServiceVersion = serviceRegistrationRepo.GetServiceVersionInternal(sessionInitInfo.ServiceName, false);

                /*if (dynamicServiceVersion != null)
                {
                    TraceHelper.TraceEvent(TraceEventType.Verbose, "[SessionLauncher] .AllocateInternalAsync: Selected dynamicServiceVersion is {0}.", dynamicServiceVersion.ToString());
                }*/

                serviceConfigFile = serviceRegistrationRepo.GetServiceRegistrationPath(sessionInitInfo.ServiceName, dynamicServiceVersion);

                // If a config file is found, update the serviceVersion that is returned to client and stored in recovery info
                if (!string.IsNullOrEmpty(serviceConfigFile))
                {
                    //TraceHelper.TraceEvent(TraceEventType.Verbose, "[SessionLauncher] .AllocateInternalAsync: serviceConfigFile is {0}.", serviceConfigFile);
                    sessionInitInfo.ServiceVersion = dynamicServiceVersion;

                    if (dynamicServiceVersion != null)
                    {
                        resourceAllocateInfo.SessionInitInfo.ServiceVersion = dynamicServiceVersion;
                    }
                }
            }

            string serviceName = ServiceRegistrationRepo.GetServiceRegistrationFileName(sessionInitInfo.ServiceName, sessionInitInfo.ServiceVersion);
            //TraceHelper.TraceEvent(TraceEventType.Verbose, "[SessionLauncher] .AllocateInternalAsync: Service name = {0}, Configuration file = {1}", serviceName, serviceConfigFile);

            // If the service is not found and user code doesn't specify
            // version, we will use the latest version. 
            if (string.IsNullOrEmpty(serviceConfigFile))
            {
                if (sessionInitInfo.ServiceVersion != null)
                {
                    //ThrowHelper.ThrowSessionFault(SOAFaultCode.ServiceVersion_NotFound, SR.SessionLauncher_ServiceVersionNotFound, startInfo.ServiceName, startInfo.ServiceVersion.ToString());
                    throw new Exception("The specified version of service can't be found!");
                }
                else
                {
                    //ThrowHelper.ThrowSessionFault(SOAFaultCode.Service_NotFound, SR.SessionLauncher_ServiceNotFound, startInfo.ServiceName);
                    throw new Exception("The specified service can't be found!");
                }
            }
            ServiceRegistration registration = ServiceRegistration.GetServiceConfigurations(serviceConfigFile);
            try
            {
                var resourceAllocateInfoResult = await this.CreateAndSubmitSessionJob(
                    sessionInitInfo,
                    callId,
                    registration,
                    resourceAllocateInfo,
                    serviceName);
                if (resourceAllocateInfoResult != null)
                {
                    return resourceAllocateInfoResult;
                }
            }
            catch (Exception e)
            {
                throw e;
            }


            return null;

        }

        protected internal virtual ServiceRegistrationRepo CreateServiceRegistrationRepo(string regPath) => new ServiceRegistrationRepo(regPath);

        public Task<ClusterInfo> GetClusterInfoAsync()
        {
            throw new System.NotImplementedException();
        }

        public Task<ResourceAllocateInfo> GrowSessionResourceAsync(Session session)
        {
            throw new System.NotImplementedException();
        }

        public Task<ResourceAllocateInfo> ShrinkSessionResourceAsync(Session session)
        {
            throw new System.NotImplementedException();
        }

        protected abstract Task<ResourceAllocateInfo> CreateAndSubmitSessionJob(
            SessionInitInfo initInfo,
            string callId,
            ServiceRegistration registration,
            ResourceAllocateInfo sessionAllocateInfo,
            string serviceName);

        public abstract Task TerminateAsync(string sessionId);
    }
}
