using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Telepathy.HostAgent.Interface
{
    public interface IHostAgent
    {
        /// <summary>
        /// Start the host agent.
        /// </summary>
        /// <returns></returns>
        Task StartAsync();
    }
}
