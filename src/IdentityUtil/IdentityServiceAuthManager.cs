// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.IdentityUtil
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Linq;
    using System.Security.Claims;
    using System.Security.Cryptography.X509Certificates;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.Threading;
    using Microsoft.IdentityModel.Logging;
    using Microsoft.IdentityModel.Tokens;

    public class IdentityServiceAuthManager : ServiceAuthorizationManager
    {
        private X509Certificate2 signingCert;
        private string issuerName;
        private readonly string audience;
        private string authority;
        private bool isDummyMgr = false;
        private ConcurrentDictionary<string, JwtInfo> jwtDic = new ConcurrentDictionary<string, JwtInfo>();

        public IdentityServiceAuthManager(string authority, string audience)
        {
            if (string.IsNullOrEmpty(authority))
            {
                isDummyMgr = true;
            }
            else
            {
                JwtSecurityTokenHandler.DefaultInboundClaimTypeMap = new Dictionary<string, string>();
                var identityServerDiscovery = IdentityUtil.GetDiscoveryResponse(authority).Result;

                IdentityModelEventSource.ShowPII = true;
                this.signingCert = new X509Certificate2(Convert.FromBase64String(identityServerDiscovery.KeySet.Keys.First().X5c.First()));
                this.issuerName = identityServerDiscovery.Issuer;
                this.audience = audience;
                this.authority = authority;
            }
        }

        protected override bool CheckAccessCore(OperationContext operationContext)
        {
            if (isDummyMgr)
            {
                return base.CheckAccessCore(operationContext);
            }

            string customData = AuthMessageHeader.ReadHeader(operationContext.RequestContext.RequestMessage);
            return this.CheckHeaderAccess(customData);
        }

        public bool CheckHeaderAccess(string header)
        {
            if (isDummyMgr)
            {
                return true;
            }

            ClaimsPrincipal principle = null;
            if (!string.IsNullOrEmpty(header) && jwtDic.ContainsKey(header))
            {
                if (jwtDic.TryGetValue(header, out JwtInfo jwtInfo))
                {
                    if (jwtInfo.expDate > DateTime.UtcNow)
                    {
                        principle = jwtInfo.principal;
                    }
                    else
                    {
                        jwtDic.TryRemove(header, out _);
                    }
                }
            }
            else
            { 
                principle = this.ValidateJwtToken(header); 
            }

            if (principle == null)
                return false;

            Thread.CurrentPrincipal = principle;
            return true;
        }

        private ClaimsPrincipal ValidateJwtToken(string jwtToken)
        {
            if (string.IsNullOrEmpty(jwtToken))
            {
                throw new FaultException(
                    MessageFault.CreateFault(
                        new FaultCode(IdentityMessageFault.FaultCode),
                        new FaultReason("Unauthenticated"),
                        new IdentityMessageFault(authority, audience)));
            }

            var parameters = new TokenValidationParameters
            {
                ValidAudience = audience,
                ValidIssuer = issuerName,
                IssuerSigningKey = new X509SecurityKey(signingCert)
            };

            SecurityToken validatedToken;
            var handler = new JwtSecurityTokenHandler();

            try
            {
                var principal = handler.ValidateToken(jwtToken, parameters, out validatedToken);
                jwtDic.TryAdd(jwtToken, new JwtInfo(validatedToken.ValidTo, principal));

                return principal;
            }
            catch
            {
                throw new FaultException(
                    MessageFault.CreateFault(
                        new FaultCode(IdentityMessageFault.FaultCode),
                        new FaultReason("Invalid Jwt"),
                        new IdentityMessageFault(authority, audience)));
            }
        }
    }

    class JwtInfo
    {
        public DateTime expDate;
        public ClaimsPrincipal principal;

        public JwtInfo(DateTime expDate, ClaimsPrincipal principal)
        {
            this.expDate = expDate;
            this.principal = principal;
        }
    }
}
