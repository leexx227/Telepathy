// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.IdentityServer.WinAuth
{
    public class WinAuthOption
    {
        // specify the Windows authentication scheme being used
        public static readonly string WindowsAuthenticationSchemeName = Microsoft.AspNetCore.Server.IISIntegration.IISDefaults.AuthenticationScheme;

        // if user uses windows auth, should we load the groups from windows
        public static bool IncludeWindowsGroups = false;

        public static readonly string WindowsAuthGrantType = "windows_auth";
    }
}
