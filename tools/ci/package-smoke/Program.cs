// Checks the three assemblies' identities, as .NET Framework 4.8 has them, from the packages.
using System.Reflection;

string[] names = ["System.Workflow.ComponentModel", "System.Workflow.Runtime", "System.Workflow.Activities"];
int failures = 0;
foreach (var name in names)
{
    var assembly = Assembly.Load(name);
    var n = assembly.GetName();
    string token = Convert.ToHexString(n.GetPublicKeyToken() ?? []).ToLowerInvariant();
    bool ok = n.Version == new Version(4, 0, 0, 0) && token == "31bf3856ad364e35";
    if (!ok) failures++;
    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {n.FullName}");
}
return failures == 0 ? 0 : 1;
