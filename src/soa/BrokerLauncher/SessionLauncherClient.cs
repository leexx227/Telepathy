// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Internal.BrokerLauncher
{
    using System;
    using System.Security.Principal;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    using Microsoft.Hpc.Scheduler.Session.Internal;
    using Microsoft.Telepathy.Session;
    using Microsoft.Telepathy.Session.Common;
    using Microsoft.Telepathy.Session.Internal;
    using Microsoft.Telepathy.IdentityUtil;

    /// <summary>
    /// Service client to connect the session launcher in headnode
    /// </summary>
    internal class SessionLauncherClient : SessionLauncherClientBase
    {
        /// <summary>
        /// Initializes a new instance of the SessionLauncherClient class by indicating the head node machine name
        /// </summary>
        /// <param name="headNode">indicating the head node</param>
        public SessionLauncherClient(string headNode, string certThumbprint, FaultException fault = null)
            : this(GetSessionLauncherUri(headNode), certThumbprint, fault)
        {
        }

        /// <summary>
        /// Initializes a new instance of the SessionLauncherClient class.
        /// </summary>
        /// <param name="uri">the session launcher EPR</param>
        public SessionLauncherClient(Uri uri, string certThumbprint, FaultException fault)
            : base(GetBinding(), GetEndpoint(uri, certThumbprint))
        {
#if HPCPACK
            // use certificate for cluster internal authentication
            this.ClientCredentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.PeerOrChainTrust;
            this.ClientCredentials.ServiceCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
            this.ClientCredentials.ClientCertificate.SetCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindByThumbprint, certThumbprint);
#endif
            if (fault != null && fault.Code.Name.Equals(IdentityMessageFault.FaultCode))
            {
                IdentityMessageFault faultDetail = fault.CreateMessageFault().GetDetail<IdentityMessageFault>();
                this.Endpoint.Behaviors.AddBehaviorFromExForClient(faultDetail).GetAwaiter().GetResult();
            }

            if (!SoaHelper.IsOnAzure())
            {
                this.ClientCredentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
            }
        }

        /// <summary>
        /// Gets the endpoint prefix.
        /// </summary>
        internal static string EndpointPrefix
        {
            get
            {
                return SessionLauncherClient.defaultEndpointPrefix;
            }
        }

        /// <summary>
        /// Generate session launcher uri from the head node machine name
        /// </summary>
        /// <param name="headNode">indicating the head node</param>
        /// <returns>returns the session launcher uri</returns>
        private static Uri GetSessionLauncherUri(string headNode)
        {
            return new Uri(SoaHelper.GetSessionLauncherInternalAddress(headNode));
        }

        /// <summary>
        /// Get binding from configuration. If failed, fallback to default
        /// </summary>
        /// <returns>Binding Data</returns>
        private static Binding GetBinding()
        {
            // This file is only used by BrokerLauncher
            return BindingHelper.HardCodedUnSecureNetTcpBinding;
        }

        /// <summary>
        /// Create the endpoint of broker
        /// </summary>
        /// <param name="uri">Uri address</param>
        /// <returns>Endpoint Address</returns>
        private static EndpointAddress GetEndpoint(Uri uri, string certThumbprint)
        {
            // This file is only used by BrokerLauncher
            //return SoaHelper.CreateInternalCertEndpointAddress(uri, certThumbprint);
            return new EndpointAddress(uri);
        }
    }
}
