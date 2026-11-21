using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;


/**
 * KAR-477: a storylet with a claim trigger starts only when an NPC claims a request - it is
 * never chosen by ordinary selection.
 *
 * flee_from_threat, respond_to_blackmail and engage_in_argument have no preconditions and
 * the default weight of 1.0, so StoryletSelector chose them over the routine: ~40 % of a city
 * was fleeing threats nobody made at any hour, and a 365-day bake (200 NPCs) drained wealth
 * 0.46 -> 0.28 and morality 0.64 -> 0.14, typing 8 of 12 groups criminal. Without them the
 * same bake keeps morality at 0.62.
 */
public class ClaimTriggeredStoryletTests
{
    private const string Json = """
    [
      { "id": "routine", "roles": [], "location": "home", "duration_minutes": 30, "weight": 0.01 },
      { "id": "wander", "roles": [], "location": "random_street", "duration_minutes": 30, "weight": 0.0, "tags": ["fallback"] },
      { "id": "rest", "roles": [], "location": "home", "duration_minutes": 30, "weight": 0.0, "tags": ["fallback"] },
      { "id": "flee", "roles": [], "claim_trigger": { "request_type": "threat", "role_match": [] },
        "location": "random_street", "duration_minutes": 20, "weight": 1000 }
    ]
    """;


    private static StoryletLibrary _library()
    {
        // LoadFromString builds no id index; the directory loader does.
        var dir = Path.Combine(Path.GetTempPath(), "tale-claim-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "test.json"), Json);
            var library = new StoryletLibrary();
            library.LoadFromDirectory(dir);
            return library;
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }


    [Fact]
    public void AClaimTriggeredStoryletIsNeverSelectedWithoutAClaim()
    {
        var library = _library();
        var selector = new StoryletSelector(library);
        var t0 = new DateTime(2024, 1, 1, 12, 0, 0);

        for (int i = 0; i < 500; ++i)
        {
            var npc = new NpcSchedule
            {
                NpcId = i, Seed = i, Role = "worker", ScheduleStep = i % 7,
                Properties = new Dictionary<string, float>()
            };
            StoryletSelector.EnsurePropertiesInitialized(npc);
            Assert.Equal("routine", selector.SelectNext(npc, t0.AddMinutes(i * 13)).Id);
        }

        // A claim still reaches it by id, the way DesSimulation starts it.
        Assert.NotNull(library.GetById("flee"));
        Assert.NotNull(library.GetById("flee").ClaimTrigger);
    }


    /** The shipped storylets: every one with a claim trigger is out of ordinary selection. */
    [Fact]
    public void NoShippedClaimTriggeredStoryletIsSelected()
    {
        string tale = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), "..", "models", "tale"));
        var library = new StoryletLibrary();
        library.LoadFromDirectory(tale);
        var claimed = library.All.Where(d => d.ClaimTrigger != null).Select(d => d.Id).ToHashSet();
        Assert.Contains("flee_from_threat", claimed);
        Assert.Contains("respond_to_blackmail", claimed);

        var selector = new StoryletSelector(library);
        var t0 = new DateTime(2024, 1, 1);
        var roles = new[] { "worker", "merchant", "socialite", "drifter", "authority", "nightworker", "hustler", "reveler" };
        for (int i = 0; i < 2000; ++i)
        {
            var npc = new NpcSchedule
            {
                NpcId = i, Seed = i, Role = roles[i % roles.Length], ScheduleStep = i / roles.Length,
                Properties = new Dictionary<string, float>()
            };
            StoryletSelector.EnsurePropertiesInitialized(npc);
            var pick = selector.SelectNext(npc, t0.AddMinutes(i * 37));
            Assert.DoesNotContain(pick.Id, claimed);
        }
    }
}
