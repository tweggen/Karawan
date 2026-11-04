using System;
using System.IO;
using System.Text.Json;
using builtin;
using engine.behave;
using Xunit;

namespace JoyceCode.Tests.builtin;


/**
 * A behaviour that only exists for a moment is saved as what it stands in for
 * (IPersistsAs). Found when a game saved while the HelloFishmonger quest car was in its
 * after-crash state - the quest car carries a Creator, so it is saved - could not be
 * loaded: AfterCrashBehavior had no constructor System.Text.Json could call.
 */
public class TransientSaveDriving : ABehavior
{
    public int Speed { get; set; } = 35;
}


public class TransientSaveCrashed : ABehavior, IPersistsAs
{
    public IBehavior Before;
    public object PersistAs => Before;
}


public class TransientBehaviorSaveTests
{

    private static JsonSerializerOptions Options() => new()
    {
        Converters = { new InterfacePointerConverter<IBehavior>() }
    };


    [Fact]
    public void ATransientBehaviourIsSavedAsTheOneItStandsIn_ForAndLoadsAsIt()
    {
        IBehavior crashed = new TransientSaveCrashed { Before = new TransientSaveDriving { Speed = 42 } };

        string json = JsonSerializer.Serialize(crashed, Options());
        Assert.Contains(typeof(TransientSaveDriving).FullName!, json);
        Assert.DoesNotContain(typeof(TransientSaveCrashed).FullName!, json);

        var loaded = JsonSerializer.Deserialize<IBehavior>(json, Options());
        var driving = Assert.IsType<TransientSaveDriving>(loaded);
        Assert.Equal(42, driving.Speed);
    }


    [Fact]
    public void AChainOfStandInsIsFollowed_AndNothingToHandBackSavesNone()
    {
        IBehavior twice = new TransientSaveCrashed { Before = new TransientSaveCrashed { Before = new TransientSaveDriving() } };
        Assert.IsType<TransientSaveDriving>(JsonSerializer.Deserialize<IBehavior>(JsonSerializer.Serialize(twice, Options()), Options()));

        // Nothing to hand back to: written as a null implementation, which now loads as null.
        IBehavior orphan = new TransientSaveCrashed { Before = null };
        string json = JsonSerializer.Serialize(orphan, Options());
        Assert.Null(JsonSerializer.Deserialize<IBehavior>(json, Options()));
        Assert.Null(JsonSerializer.Deserialize<IBehavior>(JsonSerializer.Serialize((IBehavior)null, Options()), Options()));
    }


    /**
     * The car's after-crash behaviour itself (nogame, not referenced by this test project):
     * saved as the behaviour it hands back to, and loadable from saves that already hold it.
     */
    [Fact]
    public void TheAfterCrashBehaviourIsTransient_AndLoadableFromOlderSaves()
    {
        string root = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));
        string src = File.ReadAllText(Path.Combine(root, "nogameCode/nogame/characters/car3/AfterCrashBehavior.cs"));

        Assert.Contains("class AfterCrashBehavior : ABehavior, IPersistsAs", src);
        Assert.Contains("public object PersistAs => _oldBehavior;", src);
        Assert.Matches(@"\[System\.Text\.Json\.Serialization\.JsonConstructor\]\s*public AfterCrashBehavior\(\)", src);
    }
}
