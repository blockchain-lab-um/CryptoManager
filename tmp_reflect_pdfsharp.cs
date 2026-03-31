using System;
using System.IO;
using System.Linq;
using System.Reflection;

var asmPath = @"C:\Users\adnan\.nuget\packages\pdfsharp\6.2.4\lib\net9.0\PdfSharp.dll";
var asm = Assembly.LoadFrom(asmPath);
var t = asm.GetType("PdfSharp.Pdf.Signatures.RangedStream", throwOnError: true)!;
Console.WriteLine(t.FullName);
foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                   .Where(m => m.Name.StartsWith("Read") || m.Name.StartsWith("get_") || m.Name.StartsWith("set_") || m.Name=="Seek"))
{
    Console.WriteLine($"{m.Name}({string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))}) -> {m.ReturnType.Name}");
}
