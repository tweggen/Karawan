using System.IO;
using System.Linq;
using engine.inputs;
using Xunit;
using static engine.inputs.TextInputFilter;

namespace JoyceCode.Tests.builtin.jt;


/**
 * KAR-425: control characters in typed text act as the keys they stand for and are never
 * stored. Text reaches a field already composed - SDL text input on desktop, the soft
 * keyboard's commit on Android, which for Enter is commonly "\n" - and nothing filtered it
 * since Platform._onKeyChar went with the Silk input path.
 */
public class TextInputTests
{
    [Fact]
    public void PrintableTextIsOneRun()
    {
        var p = Split("Hällo wörld 42 ✓");
        Assert.Single(p);
        Assert.Equal("Hällo wörld 42 ✓", p[0].Text);
        Assert.Empty(Split(""));
        Assert.Empty(Split(null));
    }


    [Fact]
    public void ControlCharactersBecomeActions()
    {
        var p = Split("ab\bc\u007f\td\n");
        Assert.Equal(new[]
        {
            Piece.Insert("ab"), Piece.Act(Control.Backspace), Piece.Insert("c"),
            Piece.Act(Control.Delete), Piece.Act(Control.Tab), Piece.Insert("d"), Piece.Act(Control.Enter)
        }, p);
    }


    [Fact]
    public void CarriageReturnLineFeedIsOneEnter_AndLoneOnesAreEnterToo()
    {
        Assert.Equal(new[] { Piece.Insert("x"), Piece.Act(Control.Enter) }, Split("x\r\n"));
        Assert.Equal(new[] { Piece.Act(Control.Enter), Piece.Act(Control.Enter) }, Split("\r\r"));
        Assert.Equal(new[] { Piece.Act(Control.Enter), Piece.Act(Control.Enter) }, Split("\n\r"));
    }


    [Fact]
    public void OtherControlCharactersAreDropped()
    {
        Assert.Equal(new[] { Piece.Insert("ab") }, Split("a\0\u0001\u001b\u0085" + "b"));
        Assert.All(Split("a\u0007b\u001fc").Where(x => x.Control == Control.None),
            x => Assert.DoesNotContain(x.Text, char.IsControl));
    }


    /** The widget runs both text paths through the filter; it needs a UI root to run. */
    [Fact]
    public void BothTextPathsOfTheFieldGoThroughTheFilter()
    {
        string root = Path.GetFullPath(Path.Combine(global::engine.GameRoot.PathTo("JoyceCode"), ".."));
        string widget = File.ReadAllText(Path.Combine(root, "JoyceCode/builtin/jt/InputWidget.cs"));

        Assert.Contains("engine.inputs.TextInputFilter.Split(text)", widget);
        Assert.Contains("_replaceText((int)ev.Data1, ev.Code, ev);", widget);
        Assert.Contains("_applyText(newText, ev);", widget);
        Assert.Contains("_applyText(ev.Code, ev);", widget);
        Assert.DoesNotContain("_insert(ev.Code)", widget);
        Assert.DoesNotContain("_insert(newText)", widget);
    }
}
