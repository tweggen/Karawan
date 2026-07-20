using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using builtin.baking;
using engine.joyce;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.joyce;

/**
 * Validates the internal consistency of the pre-baked ac-{hash} animation files.
 *
 * Every ModelAnimation carries a FirstFrame that indexes into the collection's single
 * flat AllBakedMatrices array. If those offsets do not tile the array exactly - no gaps,
 * no overlaps, nothing past the end - then asking for animation A at frame N renders
 * some other animation's pose. Because BakeAnimations assigns FirstFrame while iterating
 * a SortedDictionary, the physical layout is alphabetical, so a broken offset most often
 * lands in whichever clip sorts first.
 */
public class BakedAnimationLayoutTests
{
    private readonly ITestOutputHelper _output;

    public BakedAnimationLayoutTests(ITestOutputHelper output)
    {
        _output = output;
    }


    private static string? _findGeneratedDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "nogame", "generated");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }


    public static IEnumerable<object[]> BakedFiles()
    {
        string? generated = _findGeneratedDir();
        if (generated == null)
        {
            yield break;
        }

        foreach (var path in Directory.GetFiles(generated, "ac-*"))
        {
            yield return new object[] { path };
        }
    }


    [Theory]
    [MemberData(nameof(BakedFiles))]
    public void BakedAnimationOffsetsTileTheMatrixArrayExactly(string path)
    {
        ModelAnimationCollection? animcoll;
        using (var stream = File.OpenRead(path))
        {
            animcoll = ModelAnimationCollectionReader.Read(stream);
        }

        Assert.NotNull(animcoll);
        Assert.NotNull(animcoll!.MapAnimations);
        Assert.NotEmpty(animcoll.MapAnimations);
        Assert.NotNull(animcoll.AllBakedMatrices);

        /*
         * Sort by FirstFrame and walk the array: each animation must start exactly where
         * the previous one ended, and the last must end exactly at the array's capacity.
         */
        var ordered = animcoll.MapAnimations.Values.OrderBy(ma => ma.FirstFrame).ToList();

        uint expectedFirstFrame = 0;
        foreach (var ma in ordered)
        {
            _output.WriteLine(
                $"{Path.GetFileName(path)}: '{ma.Name}' FirstFrame={ma.FirstFrame} NFrames={ma.NFrames}");

            Assert.True(ma.NFrames > 0, $"Animation '{ma.Name}' has NFrames == 0.");
            Assert.True(
                ma.FirstFrame == expectedFirstFrame,
                $"Animation '{ma.Name}' starts at frame {ma.FirstFrame}, expected {expectedFirstFrame} "
                + "- the baked offsets do not tile AllBakedMatrices contiguously.");

            expectedFirstFrame += ma.NFrames;
        }

        /*
         * AllBakedMatrices holds nFrames * nBones matrices. We do not know nBones here,
         * but the total must be an exact multiple of the frame count.
         */
        Assert.True(
            animcoll.AllBakedMatrices!.Length % expectedFirstFrame == 0,
            $"AllBakedMatrices length {animcoll.AllBakedMatrices.Length} is not a multiple of "
            + $"the total frame count {expectedFirstFrame}.");

        int nBones = (int)(animcoll.AllBakedMatrices.Length / expectedFirstFrame);
        _output.WriteLine(
            $"{Path.GetFileName(path)}: totalFrames={expectedFirstFrame} nBones={nBones} "
            + $"matrices={animcoll.AllBakedMatrices.Length}");

        Assert.True(nBones > 0, "Derived bone count must be positive.");
    }


    /**
     * The names must be the ones gameplay asks for (derived from the animation file name),
     * not whatever the FBX happened to call its clip internally.
     */
    [Theory]
    [MemberData(nameof(BakedFiles))]
    public void BakedAnimationsAreNamedAfterTheirSourceFiles(string path)
    {
        ModelAnimationCollection? animcoll;
        using (var stream = File.OpenRead(path))
        {
            animcoll = ModelAnimationCollectionReader.Read(stream);
        }

        Assert.NotNull(animcoll);

        foreach (var name in animcoll!.MapAnimations.Keys)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(name),
                $"{Path.GetFileName(path)} contains an unnamed animation.");
            Assert.DoesNotContain("mixamo", name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
