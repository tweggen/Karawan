// Probe 3: the exact OpenTK spellings for the sample's call sites, so the S2b port is
// written from reflection rather than from memory.
using System;
using System.Linq;
using System.Reflection;

static class Probe3
{
    public static void Run()
    {
        var otk = typeof(OpenTK.Graphics.OpenGL.GL);
        var asm = otk.Assembly;

        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("OPENTK SPELLINGS FOR THE SAMPLE'S CALL SITES");
        Console.WriteLine(new string('=', 78));

        foreach (var stem in new[] { "BindBuffer", "Enable", "GetInteger", "CreateProgram", "CullFace" })
        {
            var ms = otk.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.StartsWith(stem, StringComparison.Ordinal))
                .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Select(p => Short(p.ParameterType)))})")
                .Distinct().OrderBy(x => x).Take(6).ToList();
            Console.WriteLine($"  {stem}:");
            foreach (var m in ms) Console.WriteLine($"      {m}");
        }

        Console.WriteLine();
        Console.WriteLine("Enum members the sample needs:");
        foreach (var (type, member) in new[]
                 {
                     ("BufferTarget", "ArrayBuffer"), ("BufferTargetARB", "ArrayBuffer"),
                     ("EnableCap", "CullFace"), ("EnableCap", "DepthTest"),
                     ("TriangleFace", "Back"), ("CullFaceMode", "Back"),
                     ("GetPName", "ActiveTexture"), ("GetPName", "CurrentProgram"),
                     ("GetPName", "ArrayBufferBinding"), ("GetPName", "Viewport"),
                 })
        {
            var t = asm.GetExportedTypes().FirstOrDefault(x => x.Name == type && x.IsEnum);
            string status = t == null ? "TYPE MISSING"
                : (Enum.GetNames(t).Contains(member) ? "ok" : "member missing");
            Console.WriteLine($"  {type,-18}.{member,-20} {status}");
        }
    }

    static string Short(Type t) => t.IsByRef ? "ref/out " + t.GetElementType()!.Name
        : t.IsPointer ? t.GetElementType()!.Name + "*" : t.Name;
}
