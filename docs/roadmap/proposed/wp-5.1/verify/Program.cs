// WP-5.1 completeness check.
//
// The generated bindings compiling proves only that they are valid C#. What decides whether
// WP-5.2's swap can succeed is whether they expose EVERY signature the call sites bind to.
// This reflects over the compiled output and checks each of the 99 required signatures, plus
// every enum type and member, against surface.json.
//
// A missing signature here is a compile error later, in Splash.OpenGL, with a worse message.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

static class Program
{
    static int Main(string[] args)
    {
        string asmPath = args[0], surfacePath = args[1];
        var asm = Assembly.LoadFrom(Path.GetFullPath(asmPath));
        using var doc = JsonDocument.Parse(File.ReadAllText(surfacePath));
        var root = doc.RootElement;

        var gl = asm.GetTypes().FirstOrDefault(t => t.Name == "GL");
        if (gl == null) { Console.Error.WriteLine("no GL type in generated assembly"); return 1; }

        // Build the set of signatures the generated GL actually offers, in the same
        // display form surface.json uses.
        var have = new HashSet<string>(StringComparer.Ordinal);
        foreach (var m in gl.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            have.Add(Sig(m));

        // Deliberate, documented differences: these two take Silk.NET.Core types
        // (INativeContext / IGLContextSource). Accepting them would keep the very
        // dependency this work removes, so the generated bindings offer
        // GetApi(Func<string,IntPtr>) and GetApi(IGLProcAddress) instead. The call
        // sites that use them must be adapted in WP-5.2 - 3 of 339.
        var byDesign = new HashSet<string>(StringComparer.Ordinal)
        {
            "GL GetApi(IGLContextSource)", "GL GetApi(INativeContext)",
        };

        int ok = 0; var missing = new List<string>(); var expected = new List<string>();
        foreach (var mem in root.GetProperty("members").EnumerateObject())
        {
            foreach (var sig in mem.Value.GetProperty("signatures").EnumerateArray())
            {
                string want = Normalise(sig.GetString()!);
                if (have.Contains(want)) ok++;
                else if (byDesign.Contains(sig.GetString()!)) expected.Add(sig.GetString()!);
                else missing.Add($"{mem.Name}: {sig.GetString()}");
            }
        }

        Console.WriteLine("=========================================================");
        Console.WriteLine("SIGNATURE COMPLETENESS");
        Console.WriteLine("=========================================================");
        Console.WriteLine($"required : {ok + missing.Count}");
        Console.WriteLine($"present  (drop-in)          : {ok}");
        Console.WriteLine($"differ by design (WP-5.2)   : {expected.Count}");
        foreach (var e in expected) Console.WriteLine("   " + e + "   -> use GetApi(Func<string,IntPtr>) / GetApi(IGLProcAddress)");
        Console.WriteLine($"UNEXPECTEDLY MISSING        : {missing.Count}");
        foreach (var m in missing) Console.WriteLine("   " + m);

        // enums
        int eOk = 0; var eMissing = new List<string>();
        foreach (var et in root.GetProperty("enums").EnumerateObject())
        {
            var t = asm.GetTypes().FirstOrDefault(x => x.Name == et.Name && x.IsEnum);
            if (t == null) { eMissing.Add($"type {et.Name}"); continue; }
            var names = Enum.GetNames(t).ToHashSet(StringComparer.Ordinal);
            foreach (var mem in et.Value.EnumerateArray())
            {
                if (names.Contains(mem.GetString()!)) eOk++;
                else eMissing.Add($"{et.Name}.{mem.GetString()}");
            }
        }
        Console.WriteLine();
        Console.WriteLine("ENUM COMPLETENESS");
        Console.WriteLine($"required : {eOk + eMissing.Count}");
        Console.WriteLine($"present  : {eOk}");
        Console.WriteLine($"MISSING  : {eMissing.Count}");
        foreach (var m in eMissing.Take(20)) Console.WriteLine("   " + m);

        // values, cross-checked against what the probe recorded from Silk
        int vOk = 0; var vBad = new List<string>();
        foreach (var kv in root.GetProperty("enumValues").EnumerateObject())
        {
            var parts = kv.Name.Split('.');
            var t = asm.GetTypes().FirstOrDefault(x => x.Name == parts[0] && x.IsEnum);
            if (t == null) continue;
            try
            {
                ulong got = Convert.ToUInt64(Enum.Parse(t, parts[1]));
                ulong want = ulong.Parse(kv.Value.GetString()!);
                if (got == want) vOk++; else vBad.Add($"{kv.Name}: generated {got}, Silk {want}");
            }
            catch { vBad.Add($"{kv.Name}: not parseable"); }
        }
        Console.WriteLine();
        Console.WriteLine("ENUM VALUE AGREEMENT WITH SILK");
        Console.WriteLine($"matching : {vOk}");
        Console.WriteLine($"DIFFERING: {vBad.Count}");
        foreach (var b in vBad.Take(20)) Console.WriteLine("   " + b);

        bool pass = missing.Count == 0 && eMissing.Count == 0 && vBad.Count == 0;
        Console.WriteLine();
        Console.WriteLine(pass ? "RESULT: complete" : "RESULT: INCOMPLETE");
        return pass ? 0 : 1;
    }

    static string Normalise(string s) => s.Replace(" ", "");

    static string Sig(MethodInfo m)
    {
        string N(Type t)
        {
            if (t.IsByRef) return N(t.GetElementType()!);
            if (t.IsPointer) return N(t.GetElementType()!) + "*";
            return t.Name switch
            {
                "Void" => "void", "Int32" => "int", "UInt32" => "uint", "Single" => "float",
                "Double" => "double", "Boolean" => "bool", "String" => "string",
                "Byte" => "byte", "SByte" => "sbyte", "Int64" => "long", "UInt64" => "ulong",
                "UIntPtr" => "nuint", "IntPtr" => "nint",
                _ => Generic(t)
            };
        }
        string Generic(Type t)
        {
            if (!t.IsGenericType) return t.Name;
            var baseName = t.Name[..t.Name.IndexOf('`')];
            return baseName + "<" + string.Join(",", t.GetGenericArguments().Select(N)) + ">";
        }
        string ps = string.Join(",", m.GetParameters().Select(p =>
            (p.IsOut ? "out" : p.ParameterType.IsByRef ? "ref" : "") + N(p.ParameterType)));
        return Normalise($"{N(m.ReturnType)} {m.Name}({ps})");
    }
}
