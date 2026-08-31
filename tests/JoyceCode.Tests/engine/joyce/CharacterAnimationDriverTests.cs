using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace JoyceCode.Tests.engine.joyce;


/**
 * Every character the game creates has something that SETS ITS ANIMATION, every frame,
 * until it takes.
 *
 * ## Why this test exists, and why the obvious version of it is useless
 *
 * A character renders from EntityCreator with no clip selected as its bind pose - a
 * T-pose. Selecting one can fail for a while after the entity exists, because ModelCache
 * attaches the FromModel component through a queued main thread action, so it is routinely
 * absent on the first behaving frame. A single call at creation time is therefore not a
 * driver but a coin toss, and an UNCHECKED single call is what made the first T-pose fix
 * (#106) a no-op twice.
 *
 * CLAUDE.md credited a drift test at this exact path with guarding the 2026-08-25 fix.
 * **That file existed in no commit on any branch** - the fix landed and the guard did not.
 * Worse, the criterion it was described as using, "the creation site names one of the three
 * drivers", was ALREADY TRUE of the site that was still broken: the niceday NPCs name an
 * EntityStrategyFactory, that strategy starts in RestStrategy, and RestStrategy attaches
 * NearbyBehavior - which drove the "E to Talk" prompt and never once called SetAnimation.
 * Their whole animation was the unretried InitialAnimName one-shot. So a test of the FIELD
 * would have passed on the day of the sighting.
 *
 * What is asserted here instead is that a creation site can REACH a class that sets an
 * animation, through the strategies and behaviours it names.
 *
 * ## Why a source scan
 *
 * The test assembly does not reference nogameCode at all - deliberately, since referencing
 * it would drag the game into every unit test - so a scan is the only instrument available.
 * That is the same limitation §7j hit with _generateQuarterFloor and §7l with the house
 * operator.
 */
public class CharacterAnimationDriverTests
{
    /**
     * How many hops of strategy-and-behaviour naming to follow.
     *
     * Three are needed by the longest real chain (site -> EntityStrategy -> RestStrategy ->
     * NearbyBehavior); four is one of slack. Raising it further makes the closure over a
     * strategy tree big enough to reach an animation driver by accident, which is the way
     * this test would stop meaning anything.
     */
    private const int MaxHops = 4;


    /**
     * Anything that could plausibly animate a character. AnimationDriver is the retry
     * itself; SetAnimation is the call it wraps, and the walking behaviours make it
     * directly.
     */
    private static bool _drivesAnAnimation(string source)
        => source.Contains("SetAnimation(") || source.Contains("AnimationDriver");


    /**
     * What a hop follows: strategies, behaviours and controllers.
     *
     * Controllers are in the list because of the PLAYER, and that is worth writing down
     * rather than quietly widening a pattern for. nogame.modules.playerhover.WalkBehavior -
     * a different class from the citizens' WalkBehavior of the same name, in a different
     * directory - contains no animation code at all; it constructs a WalkController, and the
     * controller is what selects the clip. The player's own character is therefore the one
     * whose driver is not a behaviour.
     */
    private static readonly Regex _rxNamed =
        new(@"\b([A-Z][A-Za-z0-9]*(?:Strategy|StrategyPart|Behavior|Behaviour|Controller))\b");


    /**
     * The file with its comments taken out.
     *
     * A name in a comment is not a reference, and letting one count is not a theoretical
     * worry: niceday's EntityStrategy carries a stale class comment reading "uses two
     * sub-strategies: WalkStrategy and RecoverStrategy", neither of which it has. Following
     * it walks the closure straight into the CITIZEN strategy tree and its WalkBehavior, so
     * with comments left in, deleting the niceday animation driver outright still passed -
     * on somebody else's driver, three hops away, named only in prose.
     */
    private static string _stripComments(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(source, @"//[^\n]*", " ");
    }


    private static string _nogameRoot()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        string path = Path.GetFullPath(Path.Combine(root, "..", "nogameCode"));
        Assert.True(Directory.Exists(path), $"could not find nogameCode at {path}");

        return path;
    }


    /**
     * Class name to the file that declares it. Ambiguous names - and `EntityStrategy` is
     * declared twice, once for citizens and once for niceday NPCs - are resolved in favour
     * of the file in the same directory as whoever is asking, which is what C# does with a
     * namespace-local name.
     */
    private static string _resolve(
        Dictionary<string, List<string>> byName, string name, string fromFile)
    {
        if (!byName.TryGetValue(name, out var candidates)) return null;
        if (1 == candidates.Count) return candidates[0];

        string dir = Path.GetDirectoryName(fromFile);
        return candidates.FirstOrDefault(c => Path.GetDirectoryName(c) == dir)
               ?? candidates[0];
    }


    /**
     * The object initialiser block of one `new EntityCreator` expression, brace matched -
     * an eight line window is what let §7j's exception handler drift out of a scan's sight.
     */
    private static string _initialiserAt(string source, int idx)
    {
        int open = source.IndexOf('{', idx);
        if (open < 0) return "";

        int depth = 0;
        for (int i = open; i < source.Length; ++i)
        {
            if ('{' == source[i]) ++depth;
            else if ('}' == source[i] && 0 == --depth)
            {
                return source.Substring(open, i - open + 1);
            }
        }

        return source.Substring(open);
    }


    /**
     * The names a creation site starts from: everything named in its own initialiser, plus
     * one hop through any local variable it assigns from - because three of the six sites
     * write `EntityStrategyFactory = entity => entityStrategy` and the type only appears on
     * the `TryCreate` that produced it.
     *
     * Deliberately NOT the whole enclosing file. DrivingStrategy.cs mentions WalkBehavior
     * in an unrelated ForceSpawn call twenty lines away, so a file-wide seed would let the
     * taxi passenger - the site with the weakest animation story in the tree - pass on
     * somebody else's driver.
     */
    private static HashSet<string> _seedOf(string source, string initialiser)
    {
        var seeds = new HashSet<string>(
            _rxNamed.Matches(initialiser).Select(m => m.Groups[1].Value));

        foreach (Match m in Regex.Matches(initialiser, @"=>\s*([a-z][A-Za-z0-9]*)\s*[,}]"))
        {
            string local = m.Groups[1].Value;

            foreach (Match def in Regex.Matches(
                         source, @"(?:out\s+var|var|\w+)\s+" + Regex.Escape(local) + @"\b"))
            {
                int lineStart = source.LastIndexOf('\n', def.Index) + 1;
                int lineEnd = source.IndexOf('\n', def.Index);
                if (lineEnd < 0) lineEnd = source.Length;

                foreach (Match n in _rxNamed.Matches(source[lineStart..lineEnd]))
                {
                    seeds.Add(n.Groups[1].Value);
                }
            }
        }

        return seeds;
    }


    /**
     * THE GUARD. Every EntityCreator site reaches an animation driver.
     */
    [Fact]
    public void EveryCharacterCreationSiteReachesSomethingThatSetsAnAnimation()
    {
        string root = _nogameRoot();
        var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);

        var byName = new Dictionary<string, List<string>>();
        var sources = new Dictionary<string, string>();
        foreach (var f in files)
        {
            string s = _stripComments(File.ReadAllText(f));
            sources[f] = s;

            foreach (Match m in Regex.Matches(s, @"\bclass\s+([A-Za-z_][A-Za-z0-9_]*)"))
            {
                if (!byName.TryGetValue(m.Groups[1].Value, out var l))
                {
                    byName[m.Groups[1].Value] = l = new List<string>();
                }

                l.Add(f);
            }
        }

        int nSites = 0;

        foreach (var f in files)
        {
            string source = sources[f];
            if (f.EndsWith("EntityCreator.cs")) continue;

            foreach (Match site in Regex.Matches(
                         source,
                         @"(?:new\s+EntityCreator\s*\(\s*\)|\bEntityCreator\s+\w+\s*=\s*new\s*\(\s*\))"))
            {
                ++nSites;

                string initialiser = _initialiserAt(source, site.Index);
                var frontier = _seedOf(source, initialiser);
                var seen = new HashSet<string>();
                string reached = null;

                for (int hop = 0; hop < MaxHops && null == reached; ++hop)
                {
                    var next = new HashSet<string>();

                    foreach (var name in frontier)
                    {
                        if (!seen.Add(name)) continue;

                        string decl = _resolve(byName, name, f);
                        if (null == decl) continue;

                        if (_drivesAnAnimation(sources[decl]))
                        {
                            reached = name;
                            break;
                        }

                        foreach (Match m in _rxNamed.Matches(sources[decl]))
                        {
                            next.Add(m.Groups[1].Value);
                        }
                    }

                    frontier = next;
                }

                Assert.True(null != reached,
                    $"the character created at {Path.GetFileName(f)} "
                    + $"(offset {site.Index}) can reach no class that sets an animation "
                    + $"within {MaxHops} hops of {String.Join(", ", _seedOf(source, initialiser).OrderBy(x => x))}. "
                    + "It will render in its bind pose - a T-pose - unless "
                    + "EntityCreator.InitialAnimName happens to succeed on the one attempt "
                    + "it makes, which is not something the frame it runs on can promise.");
            }
        }

        Assert.True(nSites >= 6,
            $"only {nSites} EntityCreator sites found; this scan has stopped seeing them");
    }


    /**
     * The taxi passenger has a driver of its own, and it is not IdleBehavior.
     *
     * Stated separately because the reachability test above would be satisfied by giving it
     * IdleBehavior, which would compile and would then take a ref to a Body component this
     * entity does not have - DefaultEcs hands back a reference into unused storage rather
     * than throwing. So the shape of the fix matters here and not only its existence.
     */
    [Fact]
    public void TheTaxiPassengerIsAnimatedByABehaviourThatNeedsNoBody()
    {
        string path = Path.Combine(
            _nogameRoot(), "nogame", "quests", "Taxi", "DrivingStrategy.cs");
        Assert.True(File.Exists(path), $"could not find the taxi quest at {path}");

        string source = File.ReadAllText(path);

        Assert.Contains("AnimationOnlyBehavior", source);
        Assert.DoesNotContain("new IdleBehavior", source);
    }


    /**
     * The niceday NPCs are animated by the behaviour they actually rest in.
     *
     * Absence as well as presence: a second, correct driver somewhere else would satisfy any
     * test of the outcome, and what has to hold is that the class the strategy attaches -
     * the one that is on the entity for the character's whole life - is the one that drives
     * it.
     */
    [Fact]
    public void TheRestingNicedayNpcIsAnimatedByItsOwnBehaviour()
    {
        string dir = Path.Combine(_nogameRoot(), "nogame", "npcs", "niceday");

        string nearby = File.ReadAllText(Path.Combine(dir, "NearbyBehavior.cs"));
        Assert.True(_drivesAnAnimation(nearby),
            "niceday's NearbyBehavior drives the \"E to Talk\" prompt and nothing else, so "
            + "a resting niceday NPC has no animation driver at all");

        string rest = File.ReadAllText(Path.Combine(dir, "RestStrategy.cs"));
        Assert.Contains("CharacterModelDescription = CharacterModelDescription", rest);
    }


    /**
     * A half-built character is removed from the world, not merely hidden.
     *
     * EntityCreator._createLogical runs against an entity the CALLER created and still
     * owns, so a throw part way through does not abort a creation, it freezes one: mesh and
     * transform set, no animation state, no physics, no behaviour, no strategy. It used to
     * be hidden, on the stated grounds that disposing another owner's entity risks a double
     * dispose - a reason that expired on 2026-08-29 when engine.DoomedEntitySet made
     * dooming idempotent. Hiding alone leaves one hole: if SetVisible itself throws, and the
     * inner catch there proves that was considered possible, the result is a visible,
     * behaviour-less, physics-less T-pose that stands until its fragment unloads.
     */
    [Fact]
    public void AHalfBuiltCharacterIsDoomedRatherThanOnlyHidden()
    {
        string path = Path.Combine(
            _nogameRoot(), "nogame", "characters", "EntityCreator.cs");
        string source = File.ReadAllText(path);

        /*
         * _createLogical's own catch, found by the message it logs and brace matched from
         * the catch before it, so that a comment or an added statement cannot push the doom
         * out of a fixed line window - which is exactly how §7j's scan lost sight of an
         * exception handler.
         */
        int msg = source.IndexOf("Failed to build character", StringComparison.Ordinal);
        Assert.True(msg > 0, "EntityCreator no longer reports a failed character build");

        int idx = source.LastIndexOf("catch (", msg, StringComparison.Ordinal);
        Assert.True(idx > 0, "that report is not inside a catch block any more");

        string tail = _initialiserAt(source, idx);

        Assert.Contains("AddDoomedEntity", tail);
        Assert.Contains("SetVisible", tail);
    }
}
