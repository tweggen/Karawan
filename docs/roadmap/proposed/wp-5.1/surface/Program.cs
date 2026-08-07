// WP-5.1 — resolve the EXACT Silk.NET.OpenGL surface Splash.Silk binds to.
//
// WP-5.0 established that the expensive part of generating GL bindings is not names but
// Silk's overload expansion, and that gl.xml cannot describe it. The agreed scope is to
// generate only the surface actually used - so the first job is to find out precisely what
// that is, rather than guess.
//
// A text search gives entry-point NAMES. It cannot tell you which of Silk's 6.4-average
// overloads each call site binds to, which is exactly what a generator must emit. Roslyn
// resolves each invocation to a specific IMethodSymbol, so it can.
//
// The compilation is assembled by hand from MSBuild's own resolved reference list
// (dotnet msbuild -t:ResolveReferences -getItem:ReferencePath) rather than through
// MSBuildWorkspace. MSBuildWorkspace failed to load this solution's multi-targeted sibling
// projects, and silently produced a reference-less compilation in which NOTHING resolved -
// which looks identical to "the code uses no GL". Building the compilation explicitly makes
// that failure mode impossible: no references, no compile, loud error.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

static class Program
{
    const string GlNamespace = "Silk.NET.OpenGL";

    static int Main(string[] args)
    {
        if (args.Length < 3)
        {
            Console.Error.WriteLine("usage: surface <source-dir> <refs.json> <out.json>");
            return 2;
        }
        string srcDir = args[0], refsJson = args[1], outPath = args[2];

        var sources = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .OrderBy(p => p).ToList();

        using var refDoc = JsonDocument.Parse(File.ReadAllText(refsJson));
        var refPaths = refDoc.RootElement.GetProperty("Items").GetProperty("ReferencePath")
            .EnumerateArray().Select(e => e.GetProperty("FullPath").GetString())
            .Where(p => p != null && File.Exists(p)).Distinct().ToList();

        Console.WriteLine($"sources    : {sources.Count}");
        Console.WriteLine($"references : {refPaths.Count}");
        if (refPaths.Count == 0) { Console.Error.WriteLine("no references resolved - refusing to continue"); return 1; }

        var trees = sources.Select(p =>
            CSharpSyntaxTree.ParseText(File.ReadAllText(p),
                new CSharpParseOptions(LanguageVersion.Latest), path: p)).ToList();

        var compilation = CSharpCompilation.Create("SurfaceProbe", trees,
            refPaths.Select(p => MetadataReference.CreateFromFile(p!)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));

        // Errors are tolerated (this is not a real build) but must not be so severe that
        // GL symbols stop resolving - that is the silent-zero failure mode.
        var errs = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Console.WriteLine($"diagnostics: {errs.Count} error(s)");
        foreach (var e in errs.Take(3)) Console.WriteLine("   " + e);

        var methods = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var shapes = new SortedDictionary<string, Shape>(StringComparer.Ordinal);
        var sites = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var enums = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        // member -> numeric value, so the generator does not have to reverse PascalCase
        // back into GL_UPPER_SNAKE to look the value up. Values are cross-checked against
        // gl.xml by the generator; Silk is only the mapping, never the authority.
        var enumValues = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var otherTypes = new SortedSet<string>(StringComparer.Ordinal);
        int total = 0;

        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(inv).Symbol is not IMethodSymbol m) continue;
                var ct = m.ContainingType;
                if (ct?.ContainingNamespace?.ToDisplayString() != GlNamespace) continue;
                total++;
                string key = $"{ct.Name}.{m.Name}";
                if (!methods.TryGetValue(key, out var set)) methods[key] = set = new(StringComparer.Ordinal);
                string sg = Signature(m);
                set.Add(sg);
                sites[key] = sites.GetValueOrDefault(key) + 1;
                shapes[key + "|" + sg] = Describe(m);
            }

            foreach (var node in root.DescendantNodes())
            {
                if (node is not (MemberAccessExpressionSyntax or IdentifierNameSyntax)) continue;
                var sym = model.GetSymbolInfo(node).Symbol;
                if (sym is IFieldSymbol f && f.ContainingType?.TypeKind == TypeKind.Enum
                    && f.ContainingType.ContainingNamespace?.ToDisplayString() == GlNamespace)
                {
                    if (!enums.TryGetValue(f.ContainingType.Name, out var ms))
                        enums[f.ContainingType.Name] = ms = new(StringComparer.Ordinal);
                    ms.Add(f.Name);
                    if (f.HasConstantValue && f.ConstantValue != null)
                        enumValues[$"{f.ContainingType.Name}.{f.Name}"] = Convert.ToUInt64(f.ConstantValue).ToString();
                }
                else if (sym is INamedTypeSymbol nt
                         && nt.ContainingNamespace?.ToDisplayString() == GlNamespace
                         && nt.TypeKind != TypeKind.Enum)
                {
                    otherTypes.Add(nt.Name);
                }
            }
        }

        if (total == 0)
        {
            Console.Error.WriteLine("resolved ZERO GL call sites. That is a probe failure, not a result.");
            return 1;
        }

        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine("SURFACE ACTUALLY BOUND (Roslyn-resolved)");
        Console.WriteLine(new string('=', 78));
        Console.WriteLine($"call sites into {GlNamespace} : {total}");
        Console.WriteLine($"distinct members               : {methods.Count}");
        Console.WriteLine($"distinct signatures to emit    : {methods.Values.Sum(v => v.Count)}");
        Console.WriteLine($"enum types                     : {enums.Count}");
        Console.WriteLine($"enum members                   : {enums.Values.Sum(v => v.Count)}");
        Console.WriteLine($"non-enum Silk types            : {otherTypes.Count} ({string.Join(", ", otherTypes)})");
        Console.WriteLine();
        Console.WriteLine("--- members needing more than one overload ---");
        int multi = 0;
        foreach (var (k, v) in methods.Where(kv => kv.Value.Count > 1))
        {
            multi++;
            Console.WriteLine($"  {k}  ({v.Count} overloads, {sites[k]} sites)");
            foreach (var s in v) Console.WriteLine($"      {s}");
        }
        Console.WriteLine($"({multi} of {methods.Count} members need more than one)");

        File.WriteAllText(outPath, JsonSerializer.Serialize(new
        {
            callSites = total,
            members = methods.ToDictionary(kv => kv.Key, kv => new { sites = sites[kv.Key], signatures = kv.Value.ToList() }),
            shapes = shapes.ToDictionary(kv => kv.Key, kv => kv.Value),
            enums = enums.ToDictionary(kv => kv.Key, kv => kv.Value.ToList()),
            enumValues = enumValues,
            otherTypes = otherTypes.ToList(),
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"\nwritten: {outPath}");
        return 0;
    }

    public record Param(string Name, string Type, string RefKind, bool IsEnum, bool IsPointer);
    public record Shape(string Member, string EntryPoint, string ReturnType, bool ReturnIsEnum, List<Param> Parameters);

    // Silk tags every method with [NativeApi(EntryPoint = "glXxx")]. That mapping is the one
    // thing gl.xml cannot supply - Silk renames (GetInteger -> glGetIntegerv) and merges
    // (TexParameter -> glTexParameterf) - so capture it here, once, rather than guessing from
    // the C# name later.
    static Shape Describe(IMethodSymbol m)
    {
        string entry = m.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "NativeApiAttribute")
            ?.NamedArguments.FirstOrDefault(na => na.Key == "EntryPoint").Value.Value as string;

        var f = SymbolDisplayFormat.MinimallyQualifiedFormat;
        var ps = m.Parameters.Select(p => new Param(
            p.Name,
            p.Type.ToDisplayString(f),
            p.RefKind switch { RefKind.Ref => "ref", RefKind.Out => "out", RefKind.In => "in", _ => "" },
            p.Type.TypeKind == TypeKind.Enum,
            p.Type.TypeKind == TypeKind.Pointer)).ToList();

        return new Shape(m.Name, entry ?? "", m.ReturnType.ToDisplayString(f),
                         m.ReturnType.TypeKind == TypeKind.Enum, ps);
    }

    static string Signature(IMethodSymbol m)
    {
        var f = SymbolDisplayFormat.MinimallyQualifiedFormat;
        string ps = string.Join(", ", m.Parameters.Select(p =>
            (p.RefKind switch { RefKind.Ref => "ref ", RefKind.Out => "out ", RefKind.In => "in ", _ => "" })
            + p.Type.ToDisplayString(f)));
        string gen = m.IsGenericMethod
            ? "<" + string.Join(", ", m.TypeArguments.Select(t => t.ToDisplayString(f))) + ">" : "";
        return $"{m.ReturnType.ToDisplayString(f)} {m.Name}{gen}({ps})";
    }
}
