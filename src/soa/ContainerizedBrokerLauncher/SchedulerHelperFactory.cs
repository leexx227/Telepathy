// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Internal.ContainerizedBrokerLauncher
{
    using Microsoft.Telepathy.Common.TelepathyContext;

    internal static class SchedulerHelperFactory
    {
        public static ISchedulerHelper GetSchedulerHelper(ITelepathyContext context)
        {
            if (BrokerLauncherEnvironment.Standalone)
            {
                return new DummySchedulerHelper();
            }
            else
            {
                return new SchedulerHelper(context, null);
            }
        }

        public static ISchedulerHelper GetSchedulerHelper(ITelepathyContext context, string sessionId)
        {
            return new SchedulerHelper(context, sessionId);
        }
    }
}
