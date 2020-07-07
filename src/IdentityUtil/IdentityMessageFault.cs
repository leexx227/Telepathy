// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.IdentityUtil
{
    using System;

    [Serializable]
    public class IdentityMessageFault
    {
        public const string FaultCode = "IdentityError";

        public string Authority { get; set; }
        public string ServiceScope { get; set; }

        public IdentityMessageFault(string authority, string serviceScope)
        {
            this.Authority = authority;
            this.ServiceScope = serviceScope;
        }
    }
}
