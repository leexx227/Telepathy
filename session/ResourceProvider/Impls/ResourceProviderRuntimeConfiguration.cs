// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.ResourceProvider.SessionLauncher
{
    using Microsoft.Telepathy.ResourceProvider.Impls.SessionLaunchers;
    public static class ResourceProviderRuntimeConfiguration
    {
        internal static bool AsConsole { get; set; } = false;

        internal static bool ConfigureLogging { get; set; } = false;

        internal static SchedulerType SchedulerType { get; set; } = SchedulerType.Unknown;

        internal static bool OpenAzureStorageListener => !string.IsNullOrEmpty(SessionLauncherStorageConnectionString);

        public static string SessionLauncherStorageConnectionString { get; set; }
    }
}