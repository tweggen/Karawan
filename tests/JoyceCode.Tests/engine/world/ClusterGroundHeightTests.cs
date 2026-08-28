using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using engine.streets;
using JoyceCode.Tests.engine.streets;
using Xunit;

namespace JoyceCode.Tests.engine.world;


/**
 * Who is allowed to believe a city is flat.
 *
 * ClusterDesc.AverageHeight is the height a city WOULD be at if the terrain under it
 * had been ironed flat - which it is by default, and is not once
 * joyce.DisableClusterFlattening is set. Every site that reads it directly is therefore
 * a site that silently stops working in a terrain-following city, and the failure is
 * quiet: the thing simply appears at the wrong height.
 *
 * That is not hypothetical. Streets, their colliders and the walking height were all
 * converted while the player's CAR was missed, because it hovers to
 * Loader.GetNavigationHeightAt rather than resting on anything - so it sailed over the
 * hills at a constant altitude with nothing in the logs and nothing failing.
 *
 * So the rule is a known set, not a prohibition: every reader is listed here with why it
 * is allowed to be one. A new read anywhere else fails this test and has to be argued
 * for, which is the point - the argument is what was missing.
 */
public class ClusterGroundHeightTests
{
    /**
     * Files permitted to read ClusterDesc.AverageHeight, and why.
     *
     * Broadly three reasons: it is the definition, it is genuinely a whole-city
     * quantity, or it belongs to a subsystem that has not been converted yet. The third
     * kind is a to-do list.
     */
    private static readonly Dictionary<string, string> _allowed = new()
    {
        // The definition, and the accessor that decides between it and the terrain.
        ["JoyceCode/engine/world/ClusterDesc.cs"] = "declares it and wraps it in GroundHeightAt",
        ["JoyceCode/engine/elevation/ClusterBaseElevationOperator.cs"] = "computes it",
        ["JoyceCode/engine/streets/FlatStreetHeight.cs"] = "is the source that returns it",
        ["JoyceCode/engine/streets/IStreetHeightSource.cs"] = "names it in a comment",

        // Genuinely a whole-city quantity at this site.
        ["JoyceCode/engine/streets/GenerateClusterStreetsOperator.cs"] =
            "the flat fragment floor plane, emitted only when the city really is flat",

        // Genuinely not a position.
        ["nogameCode/nogame/cities/GenerateShopsOperator.cs"] =
            "a probability-field sample coordinate, not somewhere a shop is put",

        // Fallback for an estate with no block, which should not happen.
        ["nogameCode/nogame/cities/GeneratePolytopeOperator.cs"] =
            "last resort when an estate has no quarter",

        // The block pad's own fallback when its fit is undetermined.
        ["JoyceCode/engine/streets/Quarter.cs"] =
            "the block pad answers from it when the city is flat, and falls back to it",

        // Not converted yet: the intercity network, which spans clusters and has its own
        // elevation operator.
        ["nogameCode/nogame/characters/intercity/GenerateCharacterOperator.cs"] = "intercity",
        ["nogameCode/nogame/intercity/IntercityTrackElevationOperator.cs"] = "intercity",
        ["nogameCode/nogame/intercity/Network.cs"] = "intercity",
    };


    private static string _repoRoot()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        return Path.GetFullPath(Path.Combine(root, ".."));
    }


    [Fact]
    public void OnlyKnownSitesAssumeACityIsFlat()
    {
        string root = _repoRoot();

        var found = new[] { "JoyceCode", "nogameCode" }
            .SelectMany(dir => Directory.EnumerateFiles(
                Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
            .Where(f => File.ReadAllText(f).Contains("AverageHeight"))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();

        var unexpected = found.Where(f => !_allowed.ContainsKey(f)).ToList();

        Assert.True(0 == unexpected.Count,
            "these read ClusterDesc.AverageHeight and are not on the known list:\n  "
            + String.Join("\n  ", unexpected)
            + "\n\nAverageHeight is only the height of a city that has been flattened. If "
            + "this positions something that moves - a vehicle, an NPC, a spawn, a route - "
            + "use ClusterDesc.GroundHeightAt(position), or "
            + "StreetHeightSource.GroundHeightAt(streetPoint) where a junction is in hand. "
            + "If it genuinely wants the whole-city average, add it to _allowed with the "
            + "reason.");

        /*
         * The other direction matters just as much. An entry that stops matching means a
         * subsystem was converted and the list was not updated - so the next reader is
         * told a to-do is still outstanding when it is done, or worse, a real reader
         * quietly inherits an allowance meant for a file that no longer needs it.
         */
        var stale = _allowed.Keys.Where(f => !found.Contains(f)).ToList();

        Assert.True(0 == stale.Count,
            "these are on the known list but no longer read AverageHeight - remove them:\n  "
            + String.Join("\n  ", stale));
    }


    /**
     * A flat city answers from the average, and exactly: the terrain really has been
     * ironed flat to it, so this is not an approximation.
     *
     * The terrain branch needs a booted engine and an elevation cache, so it is not
     * covered here; what is covered is that the flat path does not touch either.
     */
    [Fact]
    public void AFlatCityAnswersFromItsAverage()
    {
        var cd = StreetHarness.MakeCluster("groundheight", 500f);
        cd.StreetHeightSource = new FlatStreetHeight(cd);
        cd.AverageHeight = 41.25f;

        Assert.Equal(41.25f, cd.GroundHeightAt(new Vector3(120f, 0f, -300f)), 4);
        Assert.Equal(41.25f, cd.GroundHeightAt(new Vector3(-80f, 0f, 15f)), 4);
    }
}
