using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.joyce;

/**
 * Every declared resource must exist with EXACTLY the case it is declared with.
 *
 * WHY THIS IS NOT PARANOIA
 *
 * models/nogame.resources.json asked for man_homeless_Rig.fbx while the file on disk was
 * man_homeless_rig.fbx. Windows and macOS use case-insensitive filesystems, so it opened
 * fine and had presumably done so for as long as it had existed. The first time anything
 * ran on Linux - the CI job standing up for KI-17 - Chushi could not open it, the model
 * bake threw, and the build failed with a NullReferenceException three layers away.
 *
 * That is the whole reason GATE-C Linux was worth caring about, arriving via a different
 * route: the game's asset pipeline was broken on Linux and no check anywhere could see it.
 *
 * WHY THE COMPARISON IS DONE IN CODE
 *
 * File.Exists() answers the FILESYSTEM's question, so on Windows it returns true for the
 * mismatched case and this test would pass on the machine most likely to introduce the
 * defect. Listing the directory and comparing the names ordinally asks OUR question
 * instead, and gives the same answer on every platform.
 */
public class DeclaredResourceCasingTests
{
    private readonly ITestOutputHelper _output;

    public DeclaredResourceCasingTests(ITestOutputHelper output)
    {
        _output = output;
    }


    private static string? _findRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "models", "nogame.resources.json")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }


    /**
     * The declared uris are relative to models/, and start with "../models/". Anything
     * that is not a path into the tree - a bare name resolved by some other mechanism -
     * is not this test's business.
     */
    private static IEnumerable<string> _declaredPaths(string root)
    {
        string manifest = Path.Combine(root, "models", "nogame.resources.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(manifest));

        if (!doc.RootElement.TryGetProperty("list", out var list))
        {
            yield break;
        }

        foreach (var entry in list.EnumerateArray())
        {
            if (!entry.TryGetProperty("uri", out var uriProp))
            {
                continue;
            }

            string? uri = uriProp.GetString();
            if (string.IsNullOrEmpty(uri) || !uri!.StartsWith("../models/", StringComparison.Ordinal))
            {
                continue;
            }

            yield return uri.Substring("../".Length).Replace('/', Path.DirectorySeparatorChar);
        }
    }


    [Fact]
    public void EveryDeclaredResourceExistsWithExactlyThatCase()
    {
        string? root = _findRepoRoot();
        Assert.True(null != root, "could not locate models/nogame.resources.json");

        var wrongCase = new List<string>();
        var absent = new List<string>();
        int nChecked = 0;

        foreach (var relative in _declaredPaths(root!))
        {
            ++nChecked;
            string full = Path.Combine(root!, relative);
            string dir = Path.GetDirectoryName(full)!;
            string name = Path.GetFileName(full);

            if (!Directory.Exists(dir))
            {
                absent.Add(relative);
                continue;
            }

            /*
             * Ordinal, deliberately. Directory.EnumerateFiles hands back the names as the
             * filesystem stores them, so this compares what was declared against what
             * exists - which is the comparison File.Exists cannot make on Windows.
             */
            var names = Directory.EnumerateFiles(dir).Select(Path.GetFileName).ToList();

            if (names.Contains(name, StringComparer.Ordinal))
            {
                continue;
            }

            var sameLetters = names.FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            if (null != sameLetters)
            {
                wrongCase.Add($"{relative} is declared, but the file is '{sameLetters}'");
            }
            else
            {
                absent.Add(relative);
            }
        }

        _output.WriteLine($"{nChecked} declared paths checked");

        Assert.True(wrongCase.Count == 0,
            "Declared with the wrong case. This works on Windows and macOS and fails on Linux:\n  "
            + string.Join("\n  ", wrongCase));

        Assert.True(absent.Count == 0,
            "Declared but not present:\n  " + string.Join("\n  ", absent));
    }
}
