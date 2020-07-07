// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.IdentityUtil
{
    using System.ServiceModel.Channels;
    using System.ServiceModel.Description;
    using System.ServiceModel.Dispatcher;

    public class IdentityServiceEndpointBehavior : IEndpointBehavior
    {
        public IdentityServiceEndpointBehavior(string jwt)
        {
            this.JwtToken = jwt;
        }

        public string JwtToken { get; set; }

        public void Validate(ServiceEndpoint endpoint)
        {
        }

        public void AddBindingParameters(ServiceEndpoint endpoint, BindingParameterCollection bindingParameters)
        {
        }

        public void ApplyDispatchBehavior(ServiceEndpoint endpoint, EndpointDispatcher endpointDispatcher)
        {
        }

        public void ApplyClientBehavior(ServiceEndpoint endpoint, ClientRuntime clientRuntime)
        {
            IdentityServiceMessageInspector inspector = new IdentityServiceMessageInspector(this.JwtToken);
            clientRuntime.MessageInspectors.Add(inspector);
        }
    }
}
