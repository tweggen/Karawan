using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace JoyceCode.Tests.nogame;

/// <summary>
/// Routing names its transport type (CLAUDE.md): a Pedestrian default once
/// routed the player's car along sidewalks, and cursor and A* must use the
/// same type. Source scan: no parameter of type TransportationType has a
/// default value in JoyceCode or nogameCode (KAR-426).
/// </summary>
public class TransportTypeNoDefaultTests
{
    // A parameter "TransportationType name = value" ending at ',' or ')'; a
    // property or field initializer ends at ';' or contains braces.
    private static readonly Regex DefaultedParameter =
        new(@"TransportationType\??\s+\w+\s*=\s*[^;{}=]*?[,)]", RegexOptions.Compiled);

    private static string _repoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "JoyceCode")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }


    [Fact]
    public void NoRoutingSignatureDefaultsItsTransportType()
    {
        string root = _repoRoot();
        var hits = new[] { "JoyceCode", "nogameCode" }
            .SelectMany(d => Directory.GetFiles(Path.Combine(root, d), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (f, i, line)))
            .Where(x => !x.line.TrimStart().StartsWith("//") && DefaultedParameter.IsMatch(x.line))
            .Select(x => $"{Path.GetRelativePath(root, x.f)}:{x.i + 1}: {x.line.Trim()}")
            .ToList();

        Assert.True(hits.Count == 0, "Defaulted transport type:\n" + string.Join("\n", hits));
    }


    [Theory]
    [InlineData("        TransportationType transportType = TransportationType.Pedestrian)", true)]
    [InlineData("        TransportationType transportType = TransportationType.Pedestrian,", true)]
    [InlineData("    public TransportationType TransportType { get; set; } = TransportationType.Pedestrian;", false)]
    [InlineData("        TransportationType transportType,", false)]
    public void ThePatternSeparatesParametersFromInitialisers(string line, bool isDefaultedParameter)
    {
        Assert.Equal(isDefaultedParameter, DefaultedParameter.IsMatch(line));
    }
}
