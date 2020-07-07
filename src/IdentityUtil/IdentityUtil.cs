// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using IdentityModel.Client;

namespace Microsoft.Telepathy.IdentityUtil
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.ServiceModel.Description;
    using System.Threading.Tasks;

    public static class IdentityUtil
    {
        private static string defaultROClientId = "ro.client";

        private static string defaultClientSecret = Environment.GetEnvironmentVariable("TelepathyClientSecret");

        private static string defaultClientId = "client";

        private static string defaultWinAuthClientId = "win.client";

        private static string WinAuthGrantType = "windows_auth";

        public static string SessionLauncherApi => "SessionLauncher";

        public static string SchedulerAdapterApi => "SchedulerAdapter";

        public static string BrokerLauncherApi => "BrokerLauncher";

        public static string BrokerWorkerApi => "BrokerWorker";

        public static async Task<string> GetJwtTokenFromROAsync(string authority, string clientId, string clientSecret, string userName, string password, string scope)
        {
            var disco = await GetDiscoveryResponse(authority);
            var client = new HttpClient();
            var tokenResponse = await client.RequestPasswordTokenAsync(new PasswordTokenRequest()
            {
                Address = disco.TokenEndpoint,
                ClientId = clientId,
                ClientSecret = clientSecret,
                Scope = scope,
                UserName = userName,
                Password = password
            });

            return tokenResponse.AccessToken;
        }

        public static async Task<string> GetJwtTokenFromWinAuthAsync(string authority, string scope)
        {
            var disco = await GetDiscoveryResponse(authority);

            var httpHandler = new HttpClientHandler
            {
                UseDefaultCredentials = true
            };

            var client = new HttpClient(httpHandler);
            var tokenResponse = await client.RequestTokenAsync(new TokenRequest()
            {
                Address = disco.TokenEndpoint,
                ClientId = defaultWinAuthClientId,
                ClientSecret = defaultClientSecret,
                GrantType = WinAuthGrantType,
                Parameters =
                {
                    { "scope", scope }
                }
            });

            return tokenResponse.AccessToken;
        }

        public static async Task<string> GetJwtTokenFromClientAsync(string authority, string clientId, string clientSecret, string scope)
        {
            var disco = await GetDiscoveryResponse(authority);
            var client = new HttpClient();
            var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest()
            {
                Address = disco.TokenEndpoint,
                ClientId = clientId,
                ClientSecret = clientSecret,
                Scope = scope
            });

            return tokenResponse.AccessToken;
        }

        public static async Task<DiscoveryResponse> GetDiscoveryResponse(string authority)
        {
            var client = new HttpClient();
            var disco = await client.GetDiscoveryDocumentAsync(authority);
            if (disco.IsError)
            {
                return new DiscoveryResponse(new Exception(disco.Error), disco.Error);
            }

            return disco;
        }

        public static async Task<KeyedByTypeCollection<IEndpointBehavior>> AddBehaviorForWinAuthClient(
            this KeyedByTypeCollection<IEndpointBehavior> behaviors, string authority, string scope)
        {
            var token = await GetJwtTokenFromWinAuthAsync(authority, scope).ConfigureAwait(false);
            behaviors.Add(new IdentityServiceEndpointBehavior(token));
            return behaviors;
        }

        public static async Task<KeyedByTypeCollection<IEndpointBehavior>> AddBehaviorFromExForClient(
            this KeyedByTypeCollection<IEndpointBehavior> behaviors, IdentityMessageFault faultDetail)
        {
            var token = await GetJwtTokenFromClientAsync(faultDetail.Authority, defaultClientId, defaultClientSecret, faultDetail.ServiceScope).ConfigureAwait(false);
            behaviors.Add(new IdentityServiceEndpointBehavior(token));
            return behaviors;
        }
    }
}
