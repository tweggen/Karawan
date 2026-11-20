using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using nogame.quests.HelloFishmonger;
using Xunit;

namespace JoyceCode.Tests.nogame;


/**
 * KAR-475: a HelloFishmonger chase survives a save.
 *
 * The car used to carry a Creator of its own, so EntitySaver stored it by its persistable
 * components alone: it loaded with no model and no body, and the quest that chased it was
 * not saved at all. Now the quest entity carries HelloFishmongerQuestCreator, whose JSON
 * says where the car drives, and the car is built again on load.
 *
 * The round trip of that JSON is tested for real; the wiring that only runs in the game -
 * creator registration order, which entity carries the Creator - is pinned in source.
 */
public class HelloFishmongerPersistenceTests
{
    private static string Root => Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));
    private static string Src(string rel) => File.ReadAllText(Path.Combine(Root, rel));
    private const string Dir = "nogameCode/nogame/quests/HelloFishmonger/";


    private static JsonElement Element(string json) => JsonDocument.Parse(json).RootElement;


    [Fact]
    public void TheSavedChaseRoundTrips()
    {
        var state = new HelloFishmongerSaveState(3, new Vector2(-123.5f, 456.25f));
        var back = HelloFishmongerSaveState.FromJson(Element(state.ToJson().ToJsonString()));
        Assert.Equal(state, back);
    }


    [Theory]
    [InlineData("{}")]                                                          // saved with no car on a street
    [InlineData("{\"clusterIndex\":1,\"drivingFromX\":2}")]                     // incomplete
    [InlineData("{\"clusterIndex\":\"1\",\"drivingFromX\":2,\"drivingFromY\":3}")]
    [InlineData("\"no additional info\"")]                                      // not an object
    public void ASaveWithoutACarPositionResumesNothing(string json)
    {
        Assert.Null(HelloFishmongerSaveState.FromJson(Element(json)));
    }


    [Fact]
    public void TheCarIsNotSavedByItself_TheQuestIs()
    {
        string car = Src(Dir + "FishmongerCar.cs");
        string bindings = Src("nogameCode/nogame/modules/story/NarrationBindings.cs");

        Assert.DoesNotContain("new Creator(", car);
        Assert.DoesNotContain("CreatorId_Hardcoded", bindings);

        // The quest entity carries the chase's creator.
        string flat = Regex.Replace(bindings, @"\s+", " ");
        Assert.Contains("eQuest.Set(new engine.world.components.Creator( I.Get<engine.world.CreatorRegistry>().FindCreatorId(hfModule.QuestCreator)));", flat);
    }


    [Fact]
    public void TheCreatorWritesWhatItReads()
    {
        string creator = Src(Dir + "HelloFishmongerQuestCreator.cs");
        Assert.Contains("HelloFishmongerSaveState.FromJson(je)", creator);
        Assert.Contains("new HelloFishmongerSaveState(driving.Cluster.Index, driving.From.Pos).ToJson()", creator);
        // The car is made where the fragment is loaded, never by blocking the load.
        Assert.Contains("_ = _resumeAsync(", creator);
    }


    /**
     * Creator ids are handed out in registration order and stored in saves: the taxi's
     * creator registered first and must keep its id, so this one registers after it.
     */
    [Fact]
    public void TheChaseCreatorRegistersAfterTheTaxis()
    {
        string module = Src(Dir + "HelloFishmongerModule.cs");
        Assert.Contains("new SharedModule<nogame.quests.Taxi.TaxiNpcSpawnerModule>()", module);
        Assert.Contains("RegisterCreator(QuestCreator)", module);

        string scene = Src("nogameCode/nogame/scenes/root/Scene.cs");
        int taxi = scene.IndexOf("SharedModule<nogame.quests.Taxi.TaxiNpcSpawnerModule>", System.StringComparison.Ordinal);
        int chase = scene.IndexOf("SharedModule<nogame.quests.HelloFishmonger.HelloFishmongerModule>", System.StringComparison.Ordinal);
        Assert.True(taxi >= 0 && chase > taxi, "the root scene lists the chase module after the taxi module");
    }


    [Fact]
    public void OlderSavesLoseTheirShellCar()
    {
        string module = Src(Dir + "HelloFishmongerModule.cs");
        Assert.Contains("OnAfterLoadGame += _onAfterLoadGame", module);
        Assert.Contains("e.Get<Creator>().CreatorId == Creator.CreatorId_Hardcoded", module);
        Assert.Contains("e.Get<EntityName>().Name == FishmongerCar.Name", module);
    }
}
