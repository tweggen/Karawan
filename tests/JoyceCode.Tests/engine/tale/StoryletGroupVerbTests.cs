using System.Linq;
using engine.tale;
using Xunit;

namespace JoyceCode.Tests.engine.tale;

/// <summary>
/// Parsing tests for the Phase E4 group verb and not_in_group precondition.
/// The critical property: "group" must be EXTRACTED from postconditions at
/// parse time — if it leaks into StoryletDefinition.Postconditions,
/// ApplyPostconditions float.Parses it and crashes the simulation.
/// </summary>
public class StoryletGroupVerbTests
{
    private const string Json = """
    [
      {
        "id": "test_form_gang",
        "name": "Test Form Gang",
        "roles": ["drifter"],
        "preconditions": {
          "not_in_group": {},
          "morality": { "max": 0.4 }
        },
        "weight": 1.0,
        "duration_minutes": 90,
        "location": "current",
        "postconditions": {
          "reputation": "-0.1",
          "group": "form"
        }
      },
      {
        "id": "test_leave_gang",
        "name": "Test Leave Gang",
        "roles": ["drifter"],
        "preconditions": { "in_group": {} },
        "weight": 1.0,
        "duration_minutes": 45,
        "location": "current",
        "postconditions": { "group": "leave" }
      }
    ]
    """;

    private static StoryletLibrary Load()
    {
        var library = new StoryletLibrary();
        library.LoadFromString(Json);
        return library;
    }

    [Fact]
    public void GroupVerb_IsExtractedFromPostconditions()
    {
        var def = Load().All.First(d => d.Id == "test_form_gang");

        Assert.Equal("form", def.GroupAction);
        // MUST NOT leak into the numeric postconditions dict.
        Assert.False(def.Postconditions.ContainsKey("group"));
        Assert.Equal("-0.1", def.Postconditions["reputation"]);
    }

    [Fact]
    public void GroupVerb_LeaveParses()
    {
        var def = Load().All.First(d => d.Id == "test_leave_gang");
        Assert.Equal("leave", def.GroupAction);
        Assert.Empty(def.Postconditions);
    }

    [Fact]
    public void NotInGroup_LandsInPropertyPreconditions()
    {
        var def = Load().All.First(d => d.Id == "test_form_gang");
        Assert.True(def.PropertyPreconditions.ContainsKey("not_in_group"));
    }

    [Fact]
    public void ApplyPostconditions_DoesNotChokeOnGroupVerbStorylet()
    {
        // Regression guard for the exact crash the verb extraction prevents:
        // System.FormatException: The input string 'form' was not in a
        // correct format.
        var def = Load().All.First(d => d.Id == "test_form_gang");
        var npc = new NpcSchedule
        {
            NpcId = 1,
            Properties = new System.Collections.Generic.Dictionary<string, float>(),
            Trust = new System.Collections.Generic.Dictionary<int, float>()
        };
        var selector = new StoryletSelector(Load());
        var deltas = new System.Collections.Concurrent.ConcurrentDictionary<string, float>();

        var result = selector.ApplyPostconditions(npc, def, 90f, deltas);

        Assert.True(result.ContainsKey("reputation"));
    }
}
