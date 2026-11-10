// workflowForCore's own code (MIT). Licensed under the MIT license; see LICENSE.
//
// System.Workflow.ComponentModel's appSettings and AppContext switches. .NET Framework 4.8's assembly has these two
// classes, and the Reference Source calls them, but doesn't include them. The names and defaults are .NET Framework
// 4.8's, as tools/framework_settings.cs read them from its assembly (FrameworkData/settings.txt).
using System.Collections.Specialized;
using System.Configuration;

namespace System.Workflow.ComponentModel
{
    internal static class AppSettings
    {
        private static readonly Lazy<Values> values = new Lazy<Values>(Read);

        private sealed class Values
        {
            internal bool AllowXCode;
            internal bool DisableActivitySurrogateSelectorTypeCheck;
            internal bool DisableXOMLSerializerTypeChecking;
            internal bool DisableXOMLSerializerDefaultUnauthorizedTypes;
            internal int XOMLMaximumNestedObjectDepth = 300;
        }

        internal static bool AllowXCode => values.Value.AllowXCode;

        internal static bool DisableActivitySurrogateSelectorTypeCheck => values.Value.DisableActivitySurrogateSelectorTypeCheck;

        internal static bool DisableXOMLSerializerTypeChecking => values.Value.DisableXOMLSerializerTypeChecking;

        internal static bool DisableXOMLSerializerDefaultUnauthorizedTypes => values.Value.DisableXOMLSerializerDefaultUnauthorizedTypes;

        internal static int XOMLMaximumNestedObjectDepth => values.Value.XOMLMaximumNestedObjectDepth;

        private static Values Read()
        {
            var v = new Values();
            NameValueCollection settings = null;
            try
            {
                settings = ConfigurationManager.AppSettings;
            }
            catch (ConfigurationErrorsException)
            {
            }
            if (settings == null)
                return v;
            v.AllowXCode = Bool(settings, "microsoft:WorkflowComponentModel:AllowXCode", v.AllowXCode);
            v.DisableActivitySurrogateSelectorTypeCheck = Bool(settings, "microsoft:WorkflowComponentModel:DisableActivitySurrogateSelectorTypeCheck", v.DisableActivitySurrogateSelectorTypeCheck);
            v.DisableXOMLSerializerTypeChecking = Bool(settings, "microsoft:WorkflowComponentModel:DisableXOMLSerializerTypeChecking", v.DisableXOMLSerializerTypeChecking);
            v.DisableXOMLSerializerDefaultUnauthorizedTypes = Bool(settings, "microsoft:WorkflowComponentModel:DisableXOMLSerializerDefaultUnauthorizedTypes", v.DisableXOMLSerializerDefaultUnauthorizedTypes);
            if (int.TryParse(settings["microsoft:WorkflowComponentModel:XOMLMaximumNestedObjectDepth"], out int depth) && depth > 0)
                v.XOMLMaximumNestedObjectDepth = depth;
            return v;
        }

        private static bool Bool(NameValueCollection settings, string key, bool defaultValue)
            => bool.TryParse(settings[key], out bool value) ? value : defaultValue;
    }

    internal static class LocalAppContextSwitches
    {
        internal static bool UseLegacyHashForXomlFileChecksum
            => AppContext.TryGetSwitch("Switch.System.Workflow.ComponentModel.UseLegacyHashForXomlFileChecksum", out bool value) && value;
    }
}
