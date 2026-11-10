// Reads, from .NET Framework 4.8's System.Workflow.ComponentModel, System.Workflow.Runtime and System.Workflow.Activities,
// the configuration WF3 reads: for each class named AppSettings or LocalAppContextSwitches, its fields and the string
// and integer constants each of its methods loads (the appSettings keys, the AppContext switch names and the
// defaults). The Reference Source calls these classes but doesn't include them; workflowForCore's own AppSettings and
// LocalAppContextSwitches (src/Shared) use the same names and defaults, and FrameworkData/settings.txt records what
// this tool read. Only names and constants are written: no code.
//
// Usage: dotnet run tools/framework_settings.cs -- <folder with the three assemblies> <output.txt> [<committed.txt>]
// With a third argument, the output is compared with the committed file, and any difference fails. CI's
// workflow-framework-data job runs it on Windows against .NET Framework 4.8's GAC.
#:property NuGetAudit=false
#:property PublishAot=false
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text;

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("Usage: dotnet run tools/framework_settings.cs -- <assemblies folder> <output.txt> [<committed.txt>]");
    return 2;
}

var text = new StringBuilder();
text.Append("# .NET Framework 4.8's WF3 configuration classes, read by tools/framework_settings.cs: fields, and the strings and\n");
text.Append("# integers each method loads, in IL order. Don't edit.\n");
foreach (var name in new[] { "System.Workflow.ComponentModel", "System.Workflow.Runtime", "System.Workflow.Activities" })
{
    string dll = FindAssembly(args[0], name);
    using var stream = File.OpenRead(dll);
    using var pe = new PEReader(stream);
    var md = pe.GetMetadataReader();
    foreach (var handle in md.TypeDefinitions)
    {
        var type = md.GetTypeDefinition(handle);
        string typeName = md.GetString(type.Name);
        if (typeName != "AppSettings" && typeName != "LocalAppContextSwitches") continue;
        text.Append($"\n[{name}] {md.GetString(type.Namespace)}.{typeName}\n");
        foreach (var f in type.GetFields())
        {
            var field = md.GetFieldDefinition(f);
            string constant = field.GetDefaultValue().IsNil ? "" : " = " + Constant(md, md.GetConstant(field.GetDefaultValue()));
            text.Append($"  field {md.GetString(field.Name)}{constant}\n");
        }
        foreach (var m in type.GetMethods())
        {
            var method = md.GetMethodDefinition(m);
            if (method.RelativeVirtualAddress == 0) continue;
            var loads = Loads(md, pe.GetMethodBody(method.RelativeVirtualAddress));
            text.Append($"  method {md.GetString(method.Name)}: {string.Join(", ", loads)}\n");
        }
    }
}
File.WriteAllText(args[1], text.ToString());
Console.Write(text);
if (args.Length == 3)
{
    // Compared line by line, so that a checkout's line endings don't matter.
    var committed = File.ReadAllLines(args[2]);
    var read = text.ToString().Split('\n', StringSplitOptions.None).SkipLast(1).ToArray();
    if (!committed.SequenceEqual(read))
    {
        Console.WriteLine($"FAIL {args[2]} differs from what .NET Framework's assemblies have.");
        return 1;
    }
    Console.WriteLine($"{args[2]} is what .NET Framework's assemblies have.");
}
return 0;

static string FindAssembly(string folder, string name)
{
    string direct = Path.Combine(folder, name + ".dll");
    if (File.Exists(direct)) return direct;
    var found = Directory.GetFiles(folder, name + ".dll", SearchOption.AllDirectories);
    if (found.Length != 1) throw new FileNotFoundException($"{name}.dll: {found.Length} copies under {folder}");
    return found[0];
}

static string Constant(MetadataReader md, Constant c)
{
    var r = md.GetBlobReader(c.Value);
    return c.TypeCode switch
    {
        ConstantTypeCode.String => Quote(r.ReadUTF16(r.Length)),
        ConstantTypeCode.Boolean => r.ReadBoolean() ? "true" : "false",
        ConstantTypeCode.Int32 => r.ReadInt32().ToString(),
        _ => c.TypeCode.ToString(),
    };
}

static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

// The ldstr strings and ldc.i4 integers of a method body, and the fields it stores (stsfld), in order.
static List<string> Loads(MetadataReader md, MethodBodyBlock body)
{
    var result = new List<string>();
    var il = body.GetILReader();
    while (il.RemainingBytes > 0)
    {
        int code = il.ReadByte();
        if (code == 0xFE) code = 0xFE00 | il.ReadByte();
        var op = (ILOpCode)code;
        switch (op)
        {
            case ILOpCode.Ldstr:
                result.Add(Quote(md.GetUserString(MetadataTokens.UserStringHandle(il.ReadInt32() & 0xFFFFFF))));
                continue;
            case ILOpCode.Ldc_i4: result.Add(il.ReadInt32().ToString()); continue;
            case ILOpCode.Ldc_i4_s: result.Add(il.ReadSByte().ToString()); continue;
            case >= ILOpCode.Ldc_i4_m1 and <= ILOpCode.Ldc_i4_8: result.Add((code - (int)ILOpCode.Ldc_i4_0).ToString()); continue;
            case ILOpCode.Stsfld:
                var token = il.ReadInt32();
                var h = MetadataTokens.EntityHandle(token);
                if (h.Kind == HandleKind.FieldDefinition)
                    result.Add("->" + md.GetString(md.GetFieldDefinition((FieldDefinitionHandle)h).Name));
                continue;
            case ILOpCode.Switch:
                il.Offset += 4 * il.ReadInt32();
                continue;
        }
        il.Offset += OperandSize(op);
    }
    return result;
}

static int OperandSize(ILOpCode op) => op switch
{
    ILOpCode.Ldarg_s or ILOpCode.Ldarga_s or ILOpCode.Starg_s or ILOpCode.Ldloc_s or ILOpCode.Ldloca_s or ILOpCode.Stloc_s
        or ILOpCode.Br_s or ILOpCode.Brfalse_s or ILOpCode.Brtrue_s or ILOpCode.Beq_s or ILOpCode.Bge_s or ILOpCode.Bgt_s
        or ILOpCode.Ble_s or ILOpCode.Blt_s or ILOpCode.Bne_un_s or ILOpCode.Bge_un_s or ILOpCode.Bgt_un_s
        or ILOpCode.Ble_un_s or ILOpCode.Blt_un_s or ILOpCode.Leave_s or ILOpCode.Unaligned => 1,
    ILOpCode.Ldarg or ILOpCode.Ldarga or ILOpCode.Starg or ILOpCode.Ldloc or ILOpCode.Ldloca or ILOpCode.Stloc => 2,
    ILOpCode.Ldc_i8 or ILOpCode.Ldc_r8 => 8,
    ILOpCode.Br or ILOpCode.Brfalse or ILOpCode.Brtrue or ILOpCode.Beq or ILOpCode.Bge or ILOpCode.Bgt or ILOpCode.Ble
        or ILOpCode.Blt or ILOpCode.Bne_un or ILOpCode.Bge_un or ILOpCode.Bgt_un or ILOpCode.Ble_un or ILOpCode.Blt_un
        or ILOpCode.Leave or ILOpCode.Ldc_r4 or ILOpCode.Jmp or ILOpCode.Call or ILOpCode.Calli or ILOpCode.Callvirt
        or ILOpCode.Cpobj or ILOpCode.Ldobj or ILOpCode.Newobj or ILOpCode.Castclass or ILOpCode.Isinst
        or ILOpCode.Unbox or ILOpCode.Ldfld or ILOpCode.Ldflda or ILOpCode.Stfld or ILOpCode.Ldsfld or ILOpCode.Ldsflda
        or ILOpCode.Stobj or ILOpCode.Box or ILOpCode.Newarr or ILOpCode.Ldelema or ILOpCode.Ldelem or ILOpCode.Stelem
        or ILOpCode.Unbox_any or ILOpCode.Refanyval or ILOpCode.Mkrefany or ILOpCode.Ldtoken or ILOpCode.Ldftn
        or ILOpCode.Ldvirtftn or ILOpCode.Initobj or ILOpCode.Constrained or ILOpCode.Sizeof => 4,
    _ => 0,
};
