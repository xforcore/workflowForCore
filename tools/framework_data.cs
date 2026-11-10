// Reads the string resources of .NET Framework 4.8's System.Workflow.ComponentModel, System.Workflow.Runtime and
// System.Workflow.Activities as data, and writes them as .resx files that workflowForCore's projects compile into the
// same resources (docs/MODIFICATIONS.md, "Strings"). The Reference Source has the code that reads these resources
// (SR.cs, and the generated ExecutionStringManager and Rules.Messages classes it calls) but not the resources. Only the
// names and texts of string resources are written: no code, no IL, and none of the designers' images or dialogs.
//
// Usage: dotnet run tools/framework_data.cs -- <folder with the three assemblies> <output folder> [<committed folder>]
// The committed folder is searched for each .resx file (CI passes src/). The folder is .NET Framework's (on Windows, C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Workflow.*\v4.0_*\, or
// Microsoft.NETFramework.ReferenceAssemblies.net48's build/.NETFramework/v4.8, which carries the same resources). With
// a third argument, the output is compared with the committed files, and any difference fails. CI's
// workflow-framework-data job runs it on Windows against .NET Framework 4.8's GAC.
#:property NuGetAudit=false
#:property PublishAot=false
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Resources;
using System.Text;
using System.Xml;

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("Usage: dotnet run tools/framework_data.cs -- <assemblies folder> <output folder> [<committed folder, e.g. src>]");
    return 2;
}

// The string resources each assembly's code reads: assembly, resource name. The designers' resources (Design.*,
// ActivityDesignerResources, the rule dialogs) and the build task's (BuildTasksStrings) aren't compiled (docs/LIFT.md).
(string Assembly, string Resource)[] wanted =
[
    ("System.Workflow.ComponentModel", "System.Workflow.ComponentModel.StringResources"),
    ("System.Workflow.ComponentModel", "System.Workflow.ComponentModel.Compiler.StringResources"),
    ("System.Workflow.Runtime", "System.Workflow.Runtime.ExecutionStringManager"),
    ("System.Workflow.Activities", "System.Workflow.Activities.StringResources"),
    ("System.Workflow.Activities", "System.Workflow.Activities.Rules.Messages"),
];

string input = args[0], output = args[1];
Directory.CreateDirectory(output);
int failures = 0;
foreach (var (assembly, resource) in wanted)
{
    string dll = FindAssembly(input, assembly);
    var strings = ReadStrings(dll, resource + ".resources");
    string file = Path.Combine(output, resource + ".resx");
    File.WriteAllText(file, Resx(strings, assembly, resource));
    Console.WriteLine($"{resource}: {strings.Count} strings from {dll}");
    if (args.Length == 3)
    {
        var copies = Directory.GetFiles(args[2], resource + ".resx", SearchOption.AllDirectories);
        string committed = copies.Length == 1 ? copies[0] : Path.Combine(args[2], resource + ".resx");
        if (copies.Length != 1)
        {
            failures++;
            Console.WriteLine($"FAIL {committed} doesn't exist");
        }
        else if (!Same(ReadResx(committed), strings))
        {
            failures++;
            Console.WriteLine($"FAIL {committed} differs from .NET Framework's {resource}");
        }
    }
}
if (args.Length == 3)
    Console.WriteLine(failures == 0 ? "The committed strings are .NET Framework's." : $"{failures} files differ.");
return failures == 0 ? 0 : 1;

// The assembly in the folder, or in a GAC-style folder below it (GAC_MSIL\Name\v4.0_4.0.0.0__token\Name.dll).
static string FindAssembly(string folder, string name)
{
    string direct = Path.Combine(folder, name + ".dll");
    if (File.Exists(direct)) return direct;
    var found = Directory.GetFiles(folder, name + ".dll", SearchOption.AllDirectories);
    if (found.Length != 1) throw new FileNotFoundException($"{name}.dll: {found.Length} copies under {folder}");
    return found[0];
}

static SortedDictionary<string, string> ReadStrings(string dll, string resourceName)
{
    using var stream = File.OpenRead(dll);
    using var pe = new PEReader(stream);
    var md = pe.GetMetadataReader();
    foreach (var handle in md.ManifestResources)
    {
        var resource = md.GetManifestResource(handle);
        if (md.GetString(resource.Name) != resourceName) continue;
        if (!resource.Implementation.IsNil) throw new InvalidDataException($"{resourceName} isn't embedded in {dll}");
        // An embedded resource is at its offset in the resources directory: a 4-byte length, then the bytes.
        var directory = pe.PEHeaders.CorHeader!.ResourcesDirectory;
        var block = pe.GetSectionData(directory.RelativeVirtualAddress);
        var reader = block.GetReader((int)resource.Offset, block.Length - (int)resource.Offset);
        int length = reader.ReadInt32();
        var bytes = reader.ReadBytes(length);
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        using var resources = new ResourceReader(new MemoryStream(bytes));
        var e = resources.GetEnumerator();
        while (e.MoveNext())
        {
            if (e.Value is not string value) throw new InvalidDataException($"{resourceName}: {e.Key} isn't a string");
            result.Add((string)e.Key, value);
        }
        return result;
    }
    throw new InvalidDataException($"{dll} has no {resourceName}");
}

// The strings of a committed .resx file (an XML reader, so a checkout's line endings don't matter: a \r in a string is
// written as a character reference).
static SortedDictionary<string, string> ReadResx(string path)
{
    var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
    var doc = new XmlDocument();
    doc.Load(path);
    foreach (XmlElement data in doc.SelectNodes("/root/data")!)
        result.Add(data.GetAttribute("name"), data["value"]?.InnerText ?? "");
    return result;
}

static bool Same(SortedDictionary<string, string> a, SortedDictionary<string, string> b)
{
    bool same = a.Count == b.Count;
    foreach (var (name, value) in b)
        if (!a.TryGetValue(name, out var other) || other != value)
        {
            Console.WriteLine($"     {name}: committed {(other is null ? "missing" : Quote(other))}, .NET Framework's {Quote(value)}");
            same = false;
        }
    foreach (var name in a.Keys.Except(b.Keys))
        Console.WriteLine($"     {name}: committed, not in .NET Framework's");
    return same && !a.Keys.Except(b.Keys).Any();

    static string Quote(string s) => "\"" + s.Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
}

// A .resx file with the strings in name order, as the .NET SDK's GenerateResource reads it.
static string Resx(SortedDictionary<string, string> strings, string assembly, string resource)
{
    var buffer = new MemoryStream();
    var settings = new XmlWriterSettings { Indent = true, IndentChars = "  ", NewLineChars = "\n", NewLineHandling = NewLineHandling.Entitize, Encoding = new UTF8Encoding(false) };
    using (var w = XmlWriter.Create(buffer, settings))
    {
        w.WriteStartDocument();
        w.WriteComment($" {resource}: .NET Framework 4.8's strings, read from its {assembly}.dll by tools/framework_data.cs. Don't edit. ");
        w.WriteStartElement("root");
        Header(w, "resmimetype", "text/microsoft-resx");
        Header(w, "version", "2.0");
        Header(w, "reader", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
        Header(w, "writer", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
        foreach (var (name, value) in strings)
        {
            w.WriteStartElement("data");
            w.WriteAttributeString("name", name);
            w.WriteAttributeString("xml", "space", null, "preserve");
            w.WriteStartElement("value");
            w.WriteString(value);
            w.WriteEndElement();
            w.WriteEndElement();
        }
        w.WriteEndElement();
    }
    return Encoding.UTF8.GetString(buffer.ToArray()) + "\n";

    static void Header(XmlWriter w, string name, string value)
    {
        w.WriteStartElement("resheader");
        w.WriteAttributeString("name", name);
        w.WriteElementString("value", value);
        w.WriteEndElement();
    }
}
