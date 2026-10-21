using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExpectEngine;
using Xunit;

namespace JoyceCode.Tests.expect;

/// <summary>
/// Every TALE scenario script passes ScriptRunner's validation (KAR-433). CI
/// does not run the TALE suite, so this is where a script that would check
/// nothing is caught before it lands.
/// </summary>
public class TaleScriptsLoadTests
{
    private static string _findTaleTestsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "models", "tests", "tale");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("models/tests/tale not found above " + AppContext.BaseDirectory);
    }

    public static IEnumerable<object[]> Scripts() =>
        Directory.GetFiles(_findTaleTestsDir(), "*.json", SearchOption.AllDirectories)
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new object[] { Path.GetRelativePath(_findTaleTestsDir(), f) });


    [Theory]
    [MemberData(nameof(Scripts))]
    public void Script_Loads(string relativePath)
    {
        using var stream = File.OpenRead(Path.Combine(_findTaleTestsDir(), relativePath));
        ScriptRunner.LoadFromStream(stream);
    }


    /// <summary>The game-process scripts (tests/run-*.sh), models/tests/*.json.</summary>
    public static IEnumerable<object[]> GameScripts() =>
        Directory.GetFiles(Path.GetDirectoryName(_findTaleTestsDir())!, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => new object[] { Path.GetFileName(f) });


    [Theory]
    [MemberData(nameof(GameScripts))]
    public void GameScript_Loads(string fileName)
    {
        using var stream = File.OpenRead(Path.Combine(Path.GetDirectoryName(_findTaleTestsDir())!, fileName));
        ScriptRunner.LoadFromStream(stream);
    }


    [Fact]
    public void ThereAreScripts()
    {
        Assert.True(Scripts().Count() > 100);
        Assert.True(GameScripts().Count() >= 6);
    }
}
