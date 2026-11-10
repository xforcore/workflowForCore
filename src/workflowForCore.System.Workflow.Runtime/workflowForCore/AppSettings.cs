// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// System.Workflow.Runtime's appSettings and AppContext switches. .NET Framework 4.8's assembly has these two classes,
// and the Reference Source calls them, but doesn't include them. The names and defaults are .NET Framework 4.8's, as
// tools/framework_settings.cs read them from its assembly (FrameworkData/settings.txt).
namespace System.Workflow.Runtime
{
    internal static class AppSettings
    {
        // Whether the tracking services hash type names with their own MD5 instead of .NET's, which a FIPS policy
        // blocks. .NET's MD5 isn't subject to the FIPS policy, so they always use it.
        internal static bool FIPSRequired => false;
    }

    internal static class LocalAppContextSwitches
    {
        internal static bool UseLegacyHashForWorkflowDefinitionDispenserCacheKey
            => AppContext.TryGetSwitch("Switch.System.Workflow.Runtime.UseLegacyHashForWorkflowDefinitionDispenserCacheKey", out bool value) && value;

        internal static bool UseLegacyHashForSqlTrackingCacheKey
            => AppContext.TryGetSwitch("Switch.System.Workflow.Runtime.UseLegacyHashForSqlTrackingCacheKey", out bool value) && value;
    }
}

namespace System.Workflow.Runtime
{
    // The tracking services hash type names with MD5. Where a FIPS policy is enforced (AppSettings.FIPSRequired),
    // .NET Framework's managed MD5 throws, and they called Windows' CryptoAPI through this class instead. Both give the
    // same hash; .NET's MD5 isn't subject to that policy.
    internal static class MD5PInvokeHelper
    {
        internal static byte[] CalculateHash(byte[] data) => System.Security.Cryptography.MD5.HashData(data);
    }
}
