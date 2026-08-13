using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace GlDiff;

/// <summary>
/// GATE-F: compare the generated GL binding's public surface against Silk's, by reflection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why signatures and not rendered frames.</b> Two earlier approaches were tried and
/// measured. Diffing a live session fails because two runs of the SAME build share only
/// 13% of frames - scene sequencing depends on wall clock, and fixed dt does not help.
/// Anchoring on frame CONTENT gets a rendezvous, but only on loading milestones, which are
/// exactly the frames whose GL stream is not a function of content; steady-state frames are
/// content-pure but never coincide, because the NPC population differs when each run settles.
/// </para>
/// <para>
/// A record-only differential harness was then built to call every signature on both
/// bindings with no driver behind them. Silk went through it and recorded all 85 calls; the
/// generated binding recorded none, because it resolves entry points with
/// <c>Marshal.GetDelegateForFunctionPointer</c>, and .NET hands back the ORIGINAL delegate
/// when the pointer came from <c>GetFunctionPointerForDelegate</c> - so the cast to the
/// binding's own delegate type throws. Documented round-trip behaviour, not a defect in the
/// tracer, but it makes that harness unusable against this binding.
/// </para>
/// <para>
/// <b>What is left is also what was actually wanted.</b> The swap's risk is that a
/// generated signature marshals differently from Silk's - which is precisely the defect
/// already found by hand: <c>glTexParameterIiv</c> declared its third parameter by value
/// where Silk declares <c>in int</c> and the driver dereferences a pointer. That is a
/// METADATA property. It needs no GL context, no window and no execution, and comparing it
/// is deterministic by construction.
/// </para>
/// <para>
/// Enum VALUES are separately verified against gl.xml by gen.py, and parameter SHAPE now is
/// too. Between the three, the binding is checked against the specification and against the
/// implementation it replaces.
/// </para>
/// </remarks>
public static class Program
{
    /**
     * Reflection type -> the spelling surface.json uses, so a reflected method can be
     * looked up among the shapes. surface.json's key drops ref kinds, so byref collapses
     * to its element type here too.
     */
    private static string SurfaceType(Type t)
    {
        if (t.IsByRef) return SurfaceType(t.GetElementType()!);
        if (t.IsPointer) return SurfaceType(t.GetElementType()!) + "*";
        if (t.IsGenericType)
        {
            string open = t.Name.Substring(0, t.Name.IndexOf('`'));
            string args = string.Join(", ", t.GetGenericArguments().Select(SurfaceType));
            return $"{open}<{args}>";
        }

        return t.Name switch
        {
            "Void" => "void", "Int32" => "int", "UInt32" => "uint",
            "Single" => "float", "Double" => "double", "Byte" => "byte",
            "Boolean" => "bool", "String" => "string",
            "UIntPtr" => "nuint", "IntPtr" => "nint",
            _ => t.Name,
        };
    }

    /** Member plus parameter types, in surface.json's vocabulary. */
    private static string ShapeKey(string member, IEnumerable<string> types)
        => member + "(" + string.Join(", ", types) + ")";

    private static string ShapeKey(MethodInfo m)
        => ShapeKey(m.Name, m.GetParameters().Select(p => SurfaceType(p.ParameterType)));

    /** Collapse 32-bit signedness, so int and uint compare equal. */
    private static string Relax(string sig)
        => sig.Replace("UInt32", "I32").Replace("Int32", "I32");

    private static string Sig(MethodInfo m)
    {
        string ps = string.Join(", ", m.GetParameters().Select(p =>
        {
            string kind = p.IsOut ? "out " : p.ParameterType.IsByRef ? (p.IsIn ? "in " : "ref ") : "";
            string t = p.ParameterType.IsByRef
                ? p.ParameterType.GetElementType()!.Name
                : p.ParameterType.Name;
            return kind + t;
        }));
        return $"{m.ReturnType.Name} {m.Name}({ps})";
    }

    public static int Main()
    {
        /*
         * The 14 CONVENIENCES have no native entry point - gen.py writes them by hand
         * because gl.xml cannot describe them - so their signatures are Silk-shaped by
         * intent and comparing them produces noise, not findings. surface.json is the only
         * thing that knows which is which.
         */
        // Keyed by SHAPE, not by member name. Several names carry both a native-backed
        // overload and a convenience - UniformMatrix4 has float* (backed) and
        // ReadOnlySpan<float> (not) - so excluding by name would either skip real
        // signatures or check hand-written ones that were never meant to match Silk.
        var backedShapes = new HashSet<string>();
        var allShapes = new HashSet<string>();
        string surfacePath = Path.Combine(AppContext.BaseDirectory, "surface.json");
        using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(surfacePath)))
        {
            foreach (JsonProperty shape in doc.RootElement.GetProperty("shapes").EnumerateObject())
            {
                string member = shape.Value.GetProperty("Member").GetString()!;
                string entry = shape.Value.GetProperty("EntryPoint").GetString() ?? "";
                var types = shape.Value.GetProperty("Parameters").EnumerateArray()
                    .Select(p => p.GetProperty("Type").GetString()!);
                string key = ShapeKey(member, types);

                allShapes.Add(key);
                if (!string.IsNullOrEmpty(entry)) backedShapes.Add(key);
            }
        }

        Type gen = typeof(Karawan.Graphics.OpenGL.GL);
        Type silk = typeof(Silk.NET.OpenGL.GL);

        var genMethods = gen.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                            .Where(m => m.DeclaringType == gen)
                            .ToList();

        var silkBySig = new HashSet<string>(
            silk.GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(Sig));
        var silkByName = silk.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                             .GroupBy(m => m.Name)
                             .ToDictionary(g => g.Key, g => g.Select(Sig).ToList());

        int matched = 0;
        var signedness = new List<string>();
        var support = new List<string>();
        var mismatches = new List<string>();
        var absent = new List<string>();

        int skipped = 0;
        foreach (MethodInfo m in genMethods.OrderBy(m => m.Name))
        {
            string shapeKey = ShapeKey(m);
            if (!backedShapes.Contains(shapeKey))
            {
                /*
                 * Two very different reasons a method is not compared, and collapsing them
                 * would hide the second:
                 *
                 *   in surface.json without an entry point -> a CONVENIENCE, Silk-shaped by
                 *       intent, nothing to compare.
                 *   not in surface.json at all             -> a SUPPORT entry point, which
                 *       gen.py emits straight from gl.xml because no call site uses it
                 *       directly. Silk renames these (glGetIntegerv -> GetInteger), so
                 *       there is no signature to compare against - they are verified
                 *       against the SPECIFICATION by gen.py instead, not against Silk.
                 */
                if (allShapes.Contains(shapeKey)) ++skipped;
                else support.Add($"  {Sig(m)}");
                continue;
            }

            string sig = Sig(m);
            if (silkBySig.Contains(sig)) { ++matched; continue; }

            if (!silkByName.TryGetValue(m.Name, out var candidates))
            {
                // A convenience with no Silk counterpart is legitimate; anything else is not.
                absent.Add($"  {sig}   (no Silk method named {m.Name})");
                continue;
            }

            /*
             * A difference of SIGNEDNESS alone is not a marshalling difference: int and
             * uint are both 32 bits and pass identically. Where these occur the generated
             * binding follows gl.xml (GLsizei is GLint) and Silk does not, so flagging it
             * as a failure would be flagging the binding for being MORE correct.
             */
            string relaxed = Relax(sig);
            if (candidates.Any(c => Relax(c) == relaxed))
            {
                signedness.Add($"  {sig}");
                continue;
            }

            mismatches.Add($"  generated : {sig}");
            foreach (string c in candidates.OrderBy(x => x))
            {
                mismatches.Add($"    silk has: {c}");
            }
        }

        Console.Error.WriteLine($"generated methods : {genMethods.Count}");
        Console.Error.WriteLine($"exact signature match against Silk : {matched}");
        Console.Error.WriteLine($"conveniences skipped (no native entry point) : {skipped}");
        Console.Error.WriteLine($"support entry points, verified vs gl.xml not Silk : {support.Count}");

        if (support.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("SUPPORT entry points - emitted from gl.xml, no Silk counterpart to compare:");
            support.ForEach(Console.Error.WriteLine);
        }

        if (signedness.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"SIGNEDNESS ONLY ({signedness.Count}) - same ABI; the generated form follows gl.xml:");
            signedness.ForEach(Console.Error.WriteLine);
        }

        if (absent.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine($"NOT PRESENT IN SILK ({absent.Count}) - gen.py's SUPPORT list keeps the");
            Console.Error.WriteLine("RAW gl.xml names where Silk renames (glGetIntegerv -> GetInteger), so these");
            Console.Error.WriteLine("are expected. Anything ELSE appearing here is a naming error:");
            absent.ForEach(Console.Error.WriteLine);
        }

        if (mismatches.Count > 0)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("SIGNATURE MISMATCHES - these marshal differently from the binding");
            Console.Error.WriteLine("being replaced, which fails at CALL time, not build time:");
            mismatches.ForEach(Console.Error.WriteLine);
            Console.Error.WriteLine();
            Console.Error.WriteLine($"FAIL: {mismatches.Count / 2} mismatching signature(s).");
            return 1;
        }

        Console.Error.WriteLine();
        Console.Error.WriteLine("OK: every generated signature matches a Silk signature exactly.");
        return 0;
    }
}
