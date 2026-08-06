// WP-5.0 / WP-5.0b API probe.
//
// Answers three questions with reflection instead of estimates:
//   1. For each GL entry point Splash.Silk actually calls, how many OVERLOADS does Silk
//      expose? ADR section 11c says matching Silk's overload expansion policy is the real
//      work, not name matching - this measures it.
//   2. Which of those names exist in OpenTK 5, spelled identically?
//   3. Which of the enum types we use exist in OpenTK, spelled identically?

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;

class Program
{
    static int Main(string[] args)
    {
        string surveyPath = args.Length > 0 ? args[0] : "gl-survey.json";
        using var doc = JsonDocument.Parse(File.ReadAllText(surveyPath));
        var entryPoints = doc.RootElement.GetProperty("entry_points")
            .EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToList();
        var enumTypes = doc.RootElement.GetProperty("enum_types")
            .EnumerateObject().Select(p => p.Name).OrderBy(x => x).ToList();

        var silkGl = typeof(Silk.NET.OpenGL.GL);
        var silkAsm = silkGl.Assembly;

        // OpenTK 5 puts the desktop GL entry points on OpenTK.Graphics.OpenGL.GL.
        var otkAsm = typeof(OpenTK.Graphics.OpenGL.GL).Assembly;
        var otkGl = otkAsm.GetType("OpenTK.Graphics.OpenGL.GL");

        var silkMethods = silkGl.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.Count());
        var otkMethods = otkGl.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
            .GroupBy(m => m.Name).ToDictionary(g => g.Key, g => g.Count());

        var silkTypes = silkAsm.GetExportedTypes().Select(t => t.Name).ToHashSet();
        var silkEnums = silkAsm.GetExportedTypes().Where(t => t.IsEnum).Select(t => t.Name).ToHashSet();
        var otkTypes = otkAsm.GetExportedTypes().Select(t => t.Name).ToHashSet();
        var otkEnums = otkAsm.GetExportedTypes().Where(t => t.IsEnum).Select(t => t.Name).ToHashSet();

        Console.WriteLine(new string('=', 78));
        Console.WriteLine("ENTRY POINTS: Silk overload count, and whether OpenTK 5 has the same name");
        Console.WriteLine(new string('=', 78));
        Console.WriteLine($"{"entry point",-28} {"silk ovl",8} {"in otk",7}  {"otk ovl",8}");

        int totalSilkOverloads = 0, presentInSilk = 0, sameNameInOtk = 0;
        var missingInOtk = new List<string>();
        foreach (var ep in entryPoints)
        {
            bool inSilk = silkMethods.TryGetValue(ep, out int sOvl);
            if (!inSilk) continue;              // survey noise, not a real GL entry point
            presentInSilk++;
            totalSilkOverloads += sOvl;
            bool inOtk = otkMethods.TryGetValue(ep, out int oOvl);
            if (inOtk) sameNameInOtk++; else missingInOtk.Add(ep);
            Console.WriteLine($"{ep,-28} {sOvl,8} {(inOtk ? "yes" : "NO"),7}  {(inOtk ? oOvl.ToString() : "-"),8}");
        }

        Console.WriteLine();
        Console.WriteLine($"entry points confirmed against Silk GL : {presentInSilk}");
        Console.WriteLine($"total Silk overloads behind them       : {totalSilkOverloads}");
        Console.WriteLine($"average overloads per entry point      : {(double)totalSilkOverloads / presentInSilk:F1}");
        Console.WriteLine($"same name present in OpenTK 5          : {sameNameInOtk}/{presentInSilk}");
        Console.WriteLine($"NOT present in OpenTK 5 under that name: {missingInOtk.Count}");
        if (missingInOtk.Count > 0)
            Console.WriteLine("   " + string.Join(", ", missingInOtk));

        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("ENUM TYPES: present in Silk? present in OpenTK 5 under the same name?");
        Console.WriteLine(new string('=', 78));
        int enumInSilk = 0, enumInOtk = 0;
        var enumMissing = new List<string>();
        foreach (var et in enumTypes)
        {
            if (!silkEnums.Contains(et) && !silkTypes.Contains(et)) continue;  // survey noise
            enumInSilk++;
            bool inOtk = otkEnums.Contains(et) || otkTypes.Contains(et);
            if (inOtk) enumInOtk++; else enumMissing.Add(et);
            Console.WriteLine($"  {et,-32} otk={(inOtk ? "yes" : "NO")}");
        }
        Console.WriteLine();
        Console.WriteLine($"enum types confirmed against Silk : {enumInSilk}");
        Console.WriteLine($"same name in OpenTK 5             : {enumInOtk}/{enumInSilk}");
        if (enumMissing.Count > 0)
            Console.WriteLine("   missing: " + string.Join(", ", enumMissing));

        Console.WriteLine();
        Console.WriteLine("Silk.NET.OpenGL exported types : " + silkTypes.Count);
        Console.WriteLine("OpenTK.Graphics exported types : " + otkTypes.Count);
        Console.WriteLine("Silk GL methods (all names)    : " + silkMethods.Count);
        Console.WriteLine("Silk GL methods (all overloads): " + silkMethods.Values.Sum());
        Probe2.Run();
        Probe3.Run();
        return 0;
    }
}
