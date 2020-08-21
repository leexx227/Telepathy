

namespace Microsoft.Telepathy.ClientAPI.AuthUtil
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using IdentityModel.Client;

    public class AuthUtil
    {
        public static async Task<string> GetAccessTokenAsync(string authority)
        {
            throw new NotImplementedException();
            //var disco = await GetDiscoveryResponse(authority);
            //var client = new HttpClient();
            //var tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest()
            //{
            //    Address = disco.TokenEndpoint,
            //    ClientId = clientId,
            //    ClientSecret = clientSecret,
            //    Scope = scope
            //});

            //return tokenResponse.AccessToken;
        }

        public static async Task<DiscoveryDocumentResponse> GetDiscoveryResponse(string authority)
        {
            var client = new HttpClient();
            var disco = await client.GetDiscoveryDocumentAsync(authority);
            //if (disco.IsError)
            //{
            //    var x = new DiscoveryDocumentResponse();
            //    //return new DiscoveryDocumentResponse(new Exception(disco.Error), disco.Error);
            //}

            return disco;
        }
    }
}
