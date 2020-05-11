// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Microsoft.Telepathy.Common.TelepathyContext.ContextImpl;

namespace Microsoft.Telepathy.Common.TelepathyContext
{
    using Microsoft.Telepathy.Common.Registry;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using k8s;

    public class TelepathyContainerContext : ITelepathyContext, IDisposable
    {
        public CancellationToken CancellationToken { get; private set; }

        public IRegistry Registry => this.ClusterContext.Registry;

        public IClusterContext ClusterContext { get; }

        public TelepathyContainerContext()
        {
            this.ClusterContext = KubeContext.Default;
        }

        /// <summary>
        ///     Get the local fabric client context. This method should be called after the GetHpcContext(CancellationToken)
        ///     overload.
        /// </summary>
        /// <returns>the hpc context instance.</returns>
        public static ITelepathyContext Get()
        {
            return new TelepathyContainerContext();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                var fabricClientContext = this.ClusterContext as IDisposable;
                if (fabricClientContext != null)
                {
                    fabricClientContext.Dispose();
                }
            }
        }
    }
}
