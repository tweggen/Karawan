using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using engine.joyce;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.engine.joyce;

/**
 * Why a character renders in its bind pose - the T-pose.
 *
 * NPCs were reported standing in T-poses near street points while others walked normally.
 * The cause was not a missing clip: it was that the STANDING behaviours set their animation
 * exactly once, on their first behaving frame, and gave up if it did not take. ModelCache
 * attaches `FromModel` through `QueueMainThreadAction`, so it is routinely absent on that
 * frame - and the walking behaviours never showed the bug because they re-issue the
 * animation on every speed change and therefore heal themselves.
 *
 * The fix depends entirely on SetAnimation reporting whether it did anything, so that is
 * what these pin.
 */
public class AnimationSelectionTests
{
    private static Model _modelWith(params string[] names)
    {
        var map = new SortedDictionary<string, ModelAnimation>();
        foreach (var name in names)
        {
            map[name] = new ModelAnimation { Name = name, NFrames = 10 };
        }

        var model = new Model();
        model.AnimationCollection = new ModelAnimationCollection { MapAnimations = map };
        return model;
    }


    [Fact]
    public void SelectingAPresentClipSucceeds()
    {
        var state = new AnimationState();

        Assert.True(state.SetAnimation(_modelWith("Idle_Generic", "Walk_Male"), "Idle_Generic"));
        Assert.NotNull(state.ModelAnimation);
        Assert.Equal("Idle_Generic", state.ModelAnimation!.Name);
    }


    /**
     * The four ways to end up in a bind pose. Each returns false, and each used to be
     * indistinguishable from success at the call site.
     */
    [Fact]
    public void EveryWayOfFailingReportsFailureAndClearsTheAnimation()
    {
        var state = new AnimationState();
        var model = _modelWith("Idle_Generic");

        // no model yet - the entity has no FromModel, which is the TRANSIENT case that
        // caused the reported T-poses
        Assert.False(state.SetAnimation(null, "Idle_Generic"));
        Assert.Null(state.ModelAnimation);

        // no clip named
        Assert.False(state.SetAnimation(model, null));
        Assert.Null(state.ModelAnimation);

        // the clip is not in the pack this model was baked with
        Assert.False(state.SetAnimation(model, "Idle_HardDay"));
        Assert.Null(state.ModelAnimation);

        // the model is still the loading placeholder
        Assert.False(state.SetAnimation(new Model(), "Idle_Generic"));
        Assert.Null(state.ModelAnimation);
    }


    /**
     * A failed selection must not leave the PREVIOUS animation running either. Holding the
     * old clip would look less broken and be harder to find, and it would mean a character
     * whose model was swapped keeps animating with a foreign skeleton.
     */
    [Fact]
    public void AFailedSelectionClearsAPreviouslyGoodOne()
    {
        var state = new AnimationState();
        var model = _modelWith("Idle_Generic");

        Assert.True(state.SetAnimation(model, "Idle_Generic"));
        Assert.False(state.SetAnimation(model, "NoSuchClip"));

        Assert.Null(state.ModelAnimation);
        Assert.Equal(0, state.ModelAnimationFrame);
    }


    /**
     * The retry loop the standing behaviours now run: fail while the model is missing,
     * succeed once it arrives. This is the whole fix expressed as a sequence.
     */
    [Fact]
    public void ARetryAfterTheModelArrivesSucceeds()
    {
        var state = new AnimationState();
        bool animationSet = false;

        // frame 1: FromModel has not been attached yet
        animationSet = state.SetAnimation(null, "Idle_Generic");
        Assert.False(animationSet);

        // frame 2: it has
        animationSet = state.SetAnimation(_modelWith("Idle_Generic"), "Idle_Generic");
        Assert.True(animationSet);
        Assert.NotNull(state.ModelAnimation);
    }


    /**
     * The failure descriptions the stuck-animation report prints.
     *
     * These exist because the first attempt at this bug shipped a retry that changed
     * nothing and produced no evidence either way, costing a whole round trip. Each cause
     * must be named DISTINCTLY, because they have opposite remedies: a missing model
     * resolves itself, a clip absent from the pack never will.
     */
    [Fact]
    public void EachCauseIsDescribedDistinctly()
    {
        Assert.Contains("no model yet", AnimationState.DescribeFailure(null, "Idle_Generic"));
        Assert.Contains("no clip was named",
            AnimationState.DescribeFailure(_modelWith("Idle_Generic"), null));
        /*
         * A freshly constructed Model already HAS an AnimationCollection - it is
         * MapAnimations that is null. Worth pinning, because ModelBuilder's decision to
         * record a node as "the animations entity" tests both, and only the second one
         * ever actually discriminates.
         */
        Assert.Contains("no MapAnimations",
            AnimationState.DescribeFailure(new Model { Name = "m" }, "Idle_Generic"));
    }


    /**
     * The most useful one: it lists what the model DOES carry, so a reader can see at a
     * glance whether the wrong clip was asked for or the wrong pack was baked.
     */
    [Fact]
    public void AMissingClipListsTheOnesThatArePresent()
    {
        string described = AnimationState.DescribeFailure(
            _modelWith("Idle_HardDay", "Walk_Male"), "Idle_Generic");

        Assert.Contains("no clip 'Idle_Generic'", described);
        Assert.Contains("Idle_HardDay", described);
        Assert.Contains("Walk_Male", described);
    }
}


/**
 * The OTHER way to end up in a T-pose, which this report was not but easily could have
 * been: a pack that does not carry the clip its NPCs will ask for.
 *
 * nogame.animations.json declares one pack per (model, purpose). Every character behaviour
 * asks for an idle, a walk, a run and a death clip, so a pack missing any of them strands
 * its NPCs in whichever state uses it - and a missing IDLE strands exactly the standing
 * ones, which is what was reported.
 */
public class AnimationPackCompletenessTests
{
    private readonly ITestOutputHelper _output;

    public AnimationPackCompletenessTests(ITestOutputHelper output)
    {
        _output = output;
    }


    private static string? _findModels()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "models");
            if (File.Exists(Path.Combine(candidate, "nogame.animations.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }


    [Fact]
    public void EveryPackCarriesAnIdleWalkRunAndDeathClip()
    {
        string? models = _findModels();
        Assert.True(null != models, "could not locate models/nogame.animations.json");

        using var doc = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(models!, "nogame.animations.json")));

        var problems = new List<string>();
        int nPacks = 0;

        foreach (var entry in doc.RootElement.GetProperty("list").EnumerateArray())
        {
            string modelUrl = entry.GetProperty("modelUrl").GetString() ?? "?";
            string modelName = modelUrl.Split('/').Last();

            if (!entry.TryGetProperty("packs", out var packs))
            {
                problems.Add($"{modelName}: no packs at all");
                continue;
            }

            foreach (var pack in packs.EnumerateObject())
            {
                ++nPacks;

                /*
                 * The clip name is the source file's stem: the bake names each animation
                 * after the fbx it came from.
                 */
                var clips = (pack.Value.GetString() ?? "")
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(u => u.Trim())
                    .Select(u => u.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
                        ? u.Substring(0, u.Length - 4)
                        : u)
                    .ToList();

                void Require(string what, Func<string, bool> predicate)
                {
                    if (!clips.Any(predicate))
                    {
                        problems.Add($"{modelName} pack '{pack.Name}': no {what} clip in [{string.Join(", ", clips)}]");
                    }
                }

                Require("idle", c => c.StartsWith("Idle_", StringComparison.Ordinal));
                Require("walk", c => c.StartsWith("Walk_", StringComparison.Ordinal));
                Require("run", c => c.StartsWith("Run_", StringComparison.Ordinal));
                Require("death", c => c.StartsWith("Death_", StringComparison.Ordinal));
            }
        }

        _output.WriteLine($"{nPacks} packs checked");

        Assert.True(problems.Count == 0,
            "A pack is missing a clip its NPCs will ask for, which renders them in a "
            + "T-pose for that state:\n  " + string.Join("\n  ", problems));
    }
}
