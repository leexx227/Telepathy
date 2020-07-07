// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.IdentityServer.WinAuth
{
    using IdentityServer4.Validation;
    using Microsoft.AspNetCore.Authentication;
    using Microsoft.AspNetCore.Http;
    using System.Security.Claims;
    using System.Security.Principal;
    using System.Threading.Tasks;
    public class WinAuthGrantValidator : IExtensionGrantValidator
    {
        private HttpContext httpContext;

        public string GrantType => WinAuthOption.WindowsAuthGrantType;

        public WinAuthGrantValidator(IHttpContextAccessor httpContextAccessor)
        {
            this.httpContext = httpContextAccessor.HttpContext;
        }

        public async Task ValidateAsync(ExtensionGrantValidationContext context)
        {
            var result = await httpContext.AuthenticateAsync(WinAuthOption.WindowsAuthenticationSchemeName);
            if (result?.Principal is WindowsPrincipal wp)
            {
                context.Result = new GrantValidationResult(wp.FindFirst(ClaimTypes.PrimarySid).Value, GrantType, wp.Claims);
            }
            else
            {
                // trigger windows auth
                await httpContext.ChallengeAsync(WinAuthOption.WindowsAuthenticationSchemeName);
                context.Result = new GrantValidationResult { IsError = false, Error = null, Subject = null };
            }
        }
    }
}
