#r "C:\Users\MatheusMartins\.nuget\packages\fido2\4.0.1\lib\net8.0\Fido2.dll"
using System.Reflection;
var asm = Assembly.LoadFrom(@"C:\Users\MatheusMartins\.nuget\packages\fido2\4.0.1\lib\net8.0\Fido2.dll");
foreach (var t in new[] { "Fido2NetLib.PublicKeyCredentialRpEntity", "Fido2NetLib.PubKeyCredParam", "Fido2NetLib.Fido2User" })
{
    var type = asm.GetType(t)!;
    Console.WriteLine("== " + t);
    foreach (var c in type.GetConstructors())
        Console.WriteLine("  ctor(" + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
}
var opts = asm.GetType("Fido2NetLib.CredentialCreateOptions")!;
foreach (var p in opts.GetProperties().Where(p => p.SetMethod != null))
    Console.WriteLine(opts.Name + "." + p.Name + " : " + p.PropertyType.Name + (p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Any() ? " REQUIRED" : ""));
