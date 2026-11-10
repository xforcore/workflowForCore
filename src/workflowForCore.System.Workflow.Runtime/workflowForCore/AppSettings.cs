// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// System.Workflow.Runtime's appSettings and AppContext switches. .NET Framework 4.8's assembly has these two classes,
// and the Reference Source calls them, but doesn't include them. The names and defaults are .NET Framework 4.8's, as
// tools/framework_settings.cs read them from its assembly (src/FrameworkData/settings.txt).
namespace System.Workflow.Runtime
{
    internal static class AppSettings
    {
        private static readonly Lazy<bool> fipsRequired = new Lazy<bool>(Read);

        // Whether the tracking services hash type names with MD5PInvokeHelper (Windows' CryptoAPI on .NET Framework)
        // instead of the managed MD5, which a FIPS policy blocks there. Both give the same hash.
        internal static bool FIPSRequired => fipsRequired.Value;

        private static bool Read()
        {
            try
            {
                return bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["microsoft:WorkflowRuntime:FIPSRequired"], out bool value) && value;
            }
            catch (System.Configuration.ConfigurationErrorsException)
            {
                return false;
            }
        }
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
