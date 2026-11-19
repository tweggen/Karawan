using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace JoyceCode.Tests.engine.tale;


/**
 * KAR-476: every TALE NPC conversation the player starts must register a session
 * (TaleNarrationBindings.BeginSession) before it triggers the script. The session is what
 * the script's end reads to apply the chat (trust, "met the player"); without one the end
 * returns silently. TaleWalkBehavior - every walking NPC, most of the city - triggered
 * conversations without it, so talking to them never registered.
 *
 * The behaviours run only in the game, so this pins the call sites in source.
 */
public class ConversationSessionSourceTests
{
    private static string Root => Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));


    [Theory]
    [InlineData("nogameCode/nogame/characters/citizen/TaleWalkBehavior.cs")]
    [InlineData("nogameCode/nogame/characters/citizen/TaleConversationBehavior.cs")]
    public void EveryTaleConversationTriggerBeginsASessionFirst(string file)
    {
        string src = File.ReadAllText(Path.Combine(Root, file));
        var triggers = Regex.Matches(src, @"\.TriggerConversation\(").Select(m => m.Index).ToList();
        Assert.NotEmpty(triggers);
        foreach (int at in triggers)
        {
            int session = src.LastIndexOf("TaleNarrationBindings.BeginSession(", at, System.StringComparison.Ordinal);
            int previousTrigger = src.LastIndexOf(".TriggerConversation(", at - 1, System.StringComparison.Ordinal);
            Assert.True(session >= 0 && session > previousTrigger,
                $"{file}: TriggerConversation at {at} is not preceded by its own BeginSession");
        }
    }


    [Fact]
    public void AWalkingNpcHitMidDialogueEndsTheConversationAsViolence()
    {
        string src = File.ReadAllText(Path.Combine(Root, "nogameCode/nogame/characters/citizen/TaleWalkBehavior.cs"));
        Assert.Contains("TaleNarrationBindings.EndByViolence(_npcId, PlayerAction.None, \"hit\")", src);
        Assert.Contains("TaleNarrationBindings.EndByViolence(_npcId, PlayerAction.None, \"crash\")", src);
    }
}
