// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.IdentityServer.Cert
{
    using System.Security.Cryptography.X509Certificates;

    static class Certificate
    {
        //TODO cert can be set in azure key vault, if necessary
        private static string certPath = System.Environment.GetEnvironmentVariable("Telepathy_IDS_CertPath");

        private static string certPwd = System.Environment.GetEnvironmentVariable("Telepathy_IDS_CertPwd");

        public static X509Certificate2 Get()
        {
            return new X509Certificate2(certPath, certPwd);
        }
    }
}
