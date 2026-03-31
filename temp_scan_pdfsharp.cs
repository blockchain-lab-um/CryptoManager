using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

var asmPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages", "pdfsharp", "6.2.4", "lib", "net9.0", "PdfSharp.dll");
var asm = Assembly.LoadFrom(asmPath);

var infoType = asm.GetType("PdfSharp.Pdf.PdfDocumentInformation", throwOnError: true)!;
var setter = infoType.GetProperty("ModificationDate")!.GetSetMethod()!;
Console.WriteLine($"Setter: {setter}");

static bool MethodCalls(MethodInfo method, MethodInfo target)
{
    var body = method.GetMethodBody();
    if (body == null) return false;
    var il = body.GetILAsByteArray();
    int i = 0;
    while (i < il.Length)
    {
        OpCode op;
        ushort code = il[i++];
        if (code == 0xFE)
            op = multiByteOpCodes[il[i++]];
        else
            op = singleByteOpCodes[code];

        if (op.OperandType == OperandType.InlineMethod)
        {
            int token = BitConverter.ToInt32(il, i);
            try
            {
                var resolved = method.Module.ResolveMethod(token);
                if (resolved == target)
                    return true;
            }
            catch {}
            i += 4;
            continue;
        }

        i += OperandSize(op.OperandType, il, i);
    }
    return false;
}

static int OperandSize(OperandType operandType, byte[] il, int index) => operandType switch
{
    OperandType.InlineNone => 0,
    OperandType.ShortInlineBrTarget => 1,
    OperandType.ShortInlineI => 1,
    OperandType.ShortInlineVar => 1,
    OperandType.InlineVar => 2,
    OperandType.InlineI => 4,
    OperandType.InlineBrTarget => 4,
    OperandType.InlineField => 4,
    OperandType.InlineMethod => 4,
    OperandType.InlineSig => 4,
    OperandType.InlineString => 4,
    OperandType.InlineTok => 4,
    OperandType.InlineType => 4,
    OperandType.ShortInlineR => 4,
    OperandType.InlineI8 => 8,
    OperandType.InlineR => 8,
    OperandType.InlineSwitch => 4 + BitConverter.ToInt32(il, index) * 4,
    OperandType.InlinePhi => 0,
    _ => 0
};

var singleByteOpCodes = new OpCode[0x100];
var multiByteOpCodes = new OpCode[0x100];
foreach (var fi in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
{
    if (fi.GetValue(null) is OpCode op)
    {
        ushort v = (ushort)op.Value;
        if (v < 0x100) singleByteOpCodes[v] = op;
        else if ((v & 0xFF00) == 0xFE00) multiByteOpCodes[v & 0xFF] = op;
    }
}

foreach (var type in asm.GetTypes())
{
    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
    {
        if (MethodCalls(method, setter))
            Console.WriteLine($"{type.FullName}::{method}");
    }
}
