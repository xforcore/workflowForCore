// Checks that workflowForCore's three assemblies have the identities of .NET Framework 4.8's: name, version, culture
// and public key token, so that .NET Framework libraries and serialized workflow state bind to them (issue #1). The
// identities aren't typed in: they are read from .NET Framework's assemblies. CI passes Microsoft's reference
// assemblies for .NET Framework 4.8 (Microsoft.NETFramework.ReferenceAssemblies.net48, on every platform) and, on
// Windows, the GAC (C:\Windows\Microsoft.NET\assembly\GAC_MSIL), which is what .NET Framework loads.
//
// Usage: dotnet run tools/ci/check_assembly_identities.cs -- <folder with workflowForCore's assemblies or .nupkg files> <.NET Framework folder>...
// Prints each assembly with both identities and .NET Framework's file version, and exits with 1 on any difference.
#:property NuGetAudit=false
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length < 2 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: dotnet run tools/ci/check_assembly_identities.cs -- <assemblies or packages folder> <.NET Framework folder>...");
    return 2;
}

string[] names = ["System.Workflow.ComponentModel", "System.Workflow.Runtime", "System.Workflow.Activities"];
int failures = 0;
foreach (var name in names)
{
    var ours = Ours(args[0], name);
    if (ours is null)
    {
        failures++;
        Console.WriteLine($"FAIL {name}: not in {args[0]}");
        continue;
    }
    foreach (var folder in args.Skip(1))
    {
        var found = Directory.Exists(folder) ? Directory.GetFiles(folder, name + ".dll", SearchOption.AllDirectories) : [];
        if (found.Length != 1)
        {
            failures++;
            Console.WriteLine($"FAIL {name}: {found.Length} copies in {folder}");
            continue;
        }
        var (theirs, fileVersion) = Read(File.ReadAllBytes(found[0]));
        bool same = ours.Value.Name.Name == theirs.Name && ours.Value.Name.Version == theirs.Version
            && (ours.Value.Name.CultureName ?? "") == (theirs.CultureName ?? "") && Token(ours.Value.Name) == Token(theirs);
        if (!same) failures++;
        Console.WriteLine($"{(same ? "ok  " : "FAIL")} {ours.Value.Name.FullName}");
        Console.WriteLine($"       .NET Framework's ({found[0]}): {theirs.FullName}, file version {fileVersion}; ours {ours.Value.FileVersion}");
    }
}
Console.WriteLine(failures == 0 ? "The assemblies have .NET Framework 4.8's identities." : $"{failures} differences.");
return failures == 0 ? 0 : 1;

// workflowForCore's assembly: in the folder, or in a .nupkg's lib/net10.0 in it.
static (AssemblyName Name, string FileVersion)? Ours(string folder, string name)
{
    var direct = Directory.GetFiles(folder, name + ".dll", SearchOption.AllDirectories);
    if (direct.Length > 0) return Read(File.ReadAllBytes(direct[0]));
    foreach (var nupkg in Directory.GetFiles(folder, "*.nupkg").Where(p => !p.EndsWith(".symbols.nupkg")))
    {
        using var zip = ZipFile.OpenRead(nupkg);
        var entry = zip.Entries.FirstOrDefault(e => e.FullName.StartsWith("lib/") && e.Name == name + ".dll");
        if (entry is null) continue;
        using var s = entry.Open();
        var bytes = new MemoryStream();
        s.CopyTo(bytes);
        return Read(bytes.ToArray());
    }
    return null;
}

static (AssemblyName Name, string FileVersion) Read(byte[] bytes)
{
    using var pe = new PEReader(new MemoryStream(bytes));
    var md = pe.GetMetadataReader();
    string fileVersion = "";
    foreach (var h in md.GetAssemblyDefinition().GetCustomAttributes())
    {
        var a = md.GetCustomAttribute(h);
        if (a.Constructor.Kind != HandleKind.MemberReference) continue;
        var ctor = md.GetMemberReference((MemberReferenceHandle)a.Constructor);
        if (ctor.Parent.Kind != HandleKind.TypeReference) continue;
        if (md.GetString(md.GetTypeReference((TypeReferenceHandle)ctor.Parent).Name) != "AssemblyFileVersionAttribute") continue;
        var r = md.GetBlobReader(a.Value);
        r.ReadUInt16();
        fileVersion = r.ReadSerializedString() ?? "";
    }
    return (md.GetAssemblyDefinition().GetAssemblyName(), fileVersion);
}

static string Token(AssemblyName n) => n.GetPublicKeyToken() is { Length: > 0 } t ? Convert.ToHexString(t).ToLowerInvariant() : "null";
