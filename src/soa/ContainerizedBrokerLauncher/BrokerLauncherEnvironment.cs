// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Internal.ContainerizedBrokerLauncher
{
    internal static class BrokerLauncherEnvironment
    {
        internal static bool Standalone => string.IsNullOrEmpty(BrokerLauncherSettings.Default.SessionAddress);
    }
}
