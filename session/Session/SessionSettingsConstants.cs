namespace Microsoft.Telepathy.Session
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    public static class SessionSettingsConstants
    {
        /// <summary>
        /// the durable
        /// </summary>
        public const string Durable = "TELEPATHY_DURABLE";

        /// <summary>
        /// the ClientIdleTimeout setting.
        /// </summary>
        public const string ClientIdleTimeout = "TELEPATHY_CLIENT_IDLE_TIMEOUT";

        /// <summary>
        /// the SessionIdleTimeout setting.
        /// </summary>
        public const string SessionIdleTimeout = "TELEPATHY_SESSION_IDLE_TIMEOUT";

        /// <summary>
        /// Version of the service
        /// </summary>
        public const string ServiceVersion = "TELEPATHY_SERVICE_VERSION";

        public const string ServiceInstanceNumber = "TELEPATHY_MAX_SERVICE_INSTANCE";
    }
}
