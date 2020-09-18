
namespace Microsoft.Telepathy.ResourceProvider.Impls.AzureBatch
{
    using System;
    internal static class AzureBatchSessionIdGenerator
    {
        public static string GenerateSessionId()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        }
    }
}
