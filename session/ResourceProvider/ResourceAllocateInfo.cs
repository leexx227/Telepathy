// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ResourceProvider
{
    using Microsoft.Telepathy.Session;

    public class ResourceAllocateInfo
    {
        public SessionInitInfo SessionInitInfo { set; get; }
        public string Id { get; set; }
        public int CoresNumber { get; set; }
        public string[] ServiceInstances { get; set; }
    }
}
