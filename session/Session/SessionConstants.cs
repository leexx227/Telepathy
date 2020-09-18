// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Microsoft.Telepathy.Session
{
    public static class SessionConstants
    {
        /// <summary>
        /// Environment Variable to pass service timeout
        /// </summary>
        public static string ServiceTimeoutEnvVar => "TELEPATHY_SVC_TIMEOUT";

        /// <summary>
        /// Environment Variable to pass service concurrency number
        /// </summary>
        public static string ServiceConcurrencyEnvVar => "TELEPATHY_SVC_CONCURRENCY";

        /// <summary>
        /// Environment Variable to pass the local working directory of customer's service
        /// </summary>
        public static string TelepathyWorkingDirEnvVar => "TELEPATHY_WORKING_DIR";

        /// <summary>
        /// Environment Variable to pass the program name of customer's service
        /// </summary>
        public static string ServiceFullPathEnvVar => "TELEPATHY_SVC_FULL_PATH";

        /// <summary>
        /// Environment Variable to pass the Ip of the dispatcher
        /// </summary>
        public static string DispatcherIPEnvVar => "TELEPATHY_DISPATCHER_IP";

        /// <summary>
        /// Environment Variable to pass request prefetch count
        /// </summary>
        public static string PrefetchCountEnvVar => "TELEPATHY_PREFETCH_COUNT";

        public static string ServiceInitializeTimeoutEnvVar => "TELEPATHY_SVC_INIT_TIMEOUT";

        public static string ServiceLanguageEnvVar => "TELEPATHY_SVC_LANGUAGE";

        /// <summary>
        /// Environment Variable to pass session id to service agent
        /// </summary>
        public static string SessionIDEnvVar => "TELEPATHY_SESSION_ID";

        public static string RegistrationStoreToken => "TELEPATHY_REGISTRATION_STORE";

        public static string QueueAddressesEnvVar => "TELEPATHY_QUEUE_ADDRESSES";

        public static string SessionConfigPathEnvVar => "TELEPATHY_SESSION_CONFIG_PATH";
    }
}
