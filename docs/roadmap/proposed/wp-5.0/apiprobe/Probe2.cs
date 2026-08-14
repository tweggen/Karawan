// Follow-up probe: the two things that decide OpenTK churn.
//   A) Is OpenTK's GL static? If so, every call site changes receiver, not just the 9
//      renamed names.
//   B) What do the 9 mismatched names map to in OpenTK?
using System;
using System.Linq;
using System.Reflection;

static class Probe2
{
    public static void Run()
    {
        var silkGl = typeof(Silk.NET.OpenGL.GL);
        var otkGl = typeof(OpenTK.Graphics.OpenGL.GL);

        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("A) RECEIVER SHAPE");
        Console.WriteLine(new string('=', 78));

        var silkInst = silkGl.GetMethods(BindingFlags.Public | BindingFlags.Instance).Length;
        var silkStat = silkGl.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Length;
        var otkInst = otkGl.GetMethods(BindingFlags.Public | BindingFlags.Instance).Length;
        var otkStat = otkGl.GetMethods(BindingFlags.Public | BindingFlags.Static).Length;

        Console.WriteLine($"Silk  GL : isAbstract={silkGl.IsAbstract} isSealed={silkGl.IsSealed}  instance methods={silkInst}  static={silkStat}");
        Console.WriteLine($"OpenTK GL: isAbstract={otkGl.IsAbstract} isSealed={otkGl.IsSealed}  instance methods={otkInst}  static={otkStat}");
        Console.WriteLine($"  Silk is used as an INSTANCE   -> call sites read _gl.Foo(...)");
        Console.WriteLine($"  OpenTK GL static-only         -> {(otkInst <= 4 ? "YES" : "no")}  (<=4 means only object's inherited members)");

        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("B) THE 9 NAME MISMATCHES - what OpenTK calls them instead");
        Console.WriteLine(new string('=', 78));
        string[] missing = { "GetInternalformat", "GetProgram", "GetStringS", "PixelStore",
                             "TexParameter", "Uniform1", "Uniform3", "Uniform4", "UniformMatrix4" };
        var otkNames = otkGl.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Select(m => m.Name).Distinct().OrderBy(x => x).ToList();
        foreach (var m in missing)
        {
            var stem = m.TrimEnd('1', '3', '4');
            var cands = otkNames.Where(n => n.StartsWith(stem, StringComparison.Ordinal)).Take(8).ToList();
            Console.WriteLine($"  Silk {m,-20} -> OpenTK: {(cands.Count > 0 ? string.Join(", ", cands) : "(no prefix match)")}");
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("C) GLEnum - Silk's catch-all, which OpenTK does not have");
        Console.WriteLine(new string('=', 78));
        var glEnum = silkGl.Assembly.GetExportedTypes().FirstOrDefault(t => t.Name == "GLEnum");
        Console.WriteLine($"Silk GLEnum members: {(glEnum != null ? Enum.GetNames(glEnum).Length : 0)}");
        Console.WriteLine("Splash.OpenGL uses 43 distinct GLEnum members (from the source survey).");
        Console.WriteLine("Each becomes a specifically-typed enum under OpenTK - no single substitute exists.");
    }
}
