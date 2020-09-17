// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
namespace Microsoft.Telepathy.HostAgent.Interface
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    public interface IHostAgent
    {
        /// <summary>
        /// Start the host agent.
        /// </summary>
        /// <returns></returns>
        Task StartAsync();
    }
}
