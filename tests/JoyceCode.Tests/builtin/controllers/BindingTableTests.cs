using System.Linq;
using builtin.controllers.bindings;
using engine.inputs;
using Xunit;

namespace JoyceCode.Tests.builtin.controllers;

/**
 * WP-6.4: the action/binding layer.
 *
 * The properties worth pinning are the ones a rebinding UI depends on and that no
 * amount of playing the game would reveal quickly: that a control round-trips through
 * JSON unchanged, that rebinding REPLACES rather than adds, and that a corrupt user
 * file costs one binding rather than all of them.
 */
public class ControlTests
{
    [Theory]
    [InlineData(ScanCode.W, "Key:W")]
    [InlineData(ScanCode.Escape, "Key:Escape")]
    [InlineData(ScanCode.LeftShift, "Key:LeftShift")]
    [InlineData(ScanCode.Space, "Key:Space")]
    public void KeyControlsRoundTrip(ScanCode scanCode, string expected)
    {
        var control = Control.Key(scanCode);
        Assert.Equal(expected, control.ToString());

        Assert.True(Control.TryParse(expected, out var parsed));
        Assert.Equal(control, parsed);
        Assert.Equal(scanCode, parsed.ScanCode);
    }


    [Fact]
    public void GamepadAndMouseControlsRoundTrip()
    {
        foreach (var control in new[]
                 {
                     Control.GamepadButton("DPadUp"),
                     Control.GamepadTrigger(1),
                     Control.MouseButton(2),
                 })
        {
            Assert.True(Control.TryParse(control.ToString(), out var parsed), control.ToString());
            Assert.Equal(control, parsed);
        }
    }


    /**
     * A key is persisted by ENUM NAME, never by number. A numeric form would keep
     * parsing after a renumbering of ScanCode and silently mean a different key - the
     * exact failure a positional binding exists to prevent.
     */
    [Fact]
    public void NumericKeyFormsAreRejected()
    {
        Assert.False(Control.TryParse("Key:26", out _));
        Assert.False(Control.TryParse("Key:NotAKey", out _));
        Assert.False(Control.TryParse("Nonsense:W", out _));
        Assert.False(Control.TryParse("", out _));
        Assert.False(Control.TryParse(null, out _));
    }


    /**
     * Controls are dictionary keys on the hot path, so value equality has to be real.
     */
    [Fact]
    public void ControlsAreValueEqual()
    {
        Assert.Equal(Control.Key(ScanCode.W), Control.Key(ScanCode.W));
        Assert.NotEqual(Control.Key(ScanCode.W), Control.Key(ScanCode.A));
        Assert.Equal(Control.GamepadButton("Y"), Control.GamepadButton("Y"));
        Assert.NotEqual(Control.GamepadButton("Y"), Control.GamepadButton("X"));

        // same index, different kind - must not collide
        Assert.NotEqual(Control.GamepadTrigger(1), Control.MouseButton(1));
    }
}


public class BindingTableTests
{
    private static BindingTable _table()
    {
        var t = new BindingTable();
        t.FromJsonString("""
            {
              "interact": { "description": "Interact.", "controls": [ "Key:E", "GamepadButton:Y" ] },
              "menu":     { "controls": [ "Key:Escape" ] }
            }
            """);
        return t;
    }


    [Fact]
    public void LookupResolvesEveryBoundControl()
    {
        var t = _table();

        Assert.Equal("interact", t.ActionOf(Control.Key(ScanCode.E)));
        Assert.Equal("interact", t.ActionOf(Control.GamepadButton("Y")));
        Assert.Equal("menu", t.ActionOf(Control.Key(ScanCode.Escape)));
        Assert.Null(t.ActionOf(Control.Key(ScanCode.Z)));
    }


    [Fact]
    public void JsonRoundTripsExactly()
    {
        var t = _table();
        string json = t.ToJsonString();

        var reloaded = new BindingTable();
        Assert.Equal(0, reloaded.FromJsonString(json));

        Assert.Equal(json, reloaded.ToJsonString());
        Assert.Equal("interact", reloaded.ActionOf(Control.Key(ScanCode.E)));
        Assert.Equal("Interact.", reloaded.Find("interact")!.Description);
    }


    /**
     * The defining behaviour of a rebind. Binding a control that already drove another
     * action must REMOVE it from that action - otherwise one press fires two actions,
     * and the user who just rebound "interact" to Escape also opens the menu.
     */
    [Fact]
    public void BindingAControlStealsItFromItsPreviousAction()
    {
        var t = _table();

        t.Bind("interact", Control.Key(ScanCode.Escape));

        Assert.Equal("interact", t.ActionOf(Control.Key(ScanCode.Escape)));
        Assert.DoesNotContain(Control.Key(ScanCode.Escape), t.Find("menu")!.Controls);
        Assert.Empty(t.Find("menu")!.Controls);
    }


    [Fact]
    public void RebindingKeepsTheOtherControlsOfAnAction()
    {
        var t = _table();

        t.Bind("interact", Control.Key(ScanCode.R));

        // gamepad binding for the same action is untouched
        Assert.Equal("interact", t.ActionOf(Control.GamepadButton("Y")));
        Assert.Equal("interact", t.ActionOf(Control.Key(ScanCode.R)));
    }


    [Fact]
    public void UnbindRemovesOnlyThatControl()
    {
        var t = _table();

        t.Unbind("interact", Control.Key(ScanCode.E));

        Assert.Null(t.ActionOf(Control.Key(ScanCode.E)));
        Assert.Equal("interact", t.ActionOf(Control.GamepadButton("Y")));
    }


    /**
     * A binding file is user-editable and may come from an older build. One bad entry
     * must cost one binding, not the whole table.
     */
    [Fact]
    public void CorruptEntriesAreSkippedNotFatal()
    {
        var t = new BindingTable();
        int nSkipped = t.FromJsonString("""
            {
              "good":  { "controls": [ "Key:E" ] },
              "bad":   { "controls": [ "Key:NotAKey", "GamepadButton:Y" ] },
              "worse": "this should be an object"
            }
            """);

        Assert.Equal(2, nSkipped);                                  // the bad key and the bad action
        Assert.Equal("good", t.ActionOf(Control.Key(ScanCode.E)));  // survivors intact
        Assert.Equal("bad", t.ActionOf(Control.GamepadButton("Y"))); // partial entry keeps what parsed
    }


    [Fact]
    public void CaptureIsOffByDefaultAndTogglesCleanly()
    {
        var t = _table();

        Assert.False(t.IsCapturing);
        t.BeginCapture();
        Assert.True(t.IsCapturing);
        t.EndCapture();
        Assert.False(t.IsCapturing);
    }


    /**
     * Actions is a snapshot: a caller iterating it while a rebinding UI writes must not
     * see the collection change underneath.
     */
    [Fact]
    public void ActionsIsASnapshot()
    {
        var t = _table();
        var before = t.Actions;

        t.Bind("newaction", Control.Key(ScanCode.P));

        Assert.Equal(2, before.Count);
        Assert.Equal(3, t.Actions.Count);
    }
}


public class InputModifierTests
{
    private const float Tol = 1e-5f;

    /**
     * The curve modifier must reproduce InputController.StickTransfer EXACTLY -
     * sign(x) * |x^4| - because WP-6.4's follow-up replaces that call site with this,
     * and a response curve that is merely close would change how the game feels
     * without failing anything.
     */
    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(-0.5f)]
    [InlineData(1f)]
    [InlineData(-1f)]
    public void CurveMatchesStickTransfer(float x)
    {
        float expected = System.Single.Sign(x) * System.Single.Abs(x * x * x * x);
        float actual = new CurveModifier(4f).Apply(x);
        Assert.True(System.Math.Abs(expected - actual) < Tol, $"x={x}: {expected} vs {actual}");
    }


    /**
     * And the range modifier must reproduce the trigger convention: SDL's -1..1 onto
     * the 0..255 the controller state stores.
     */
    [Theory]
    [InlineData(-1f, 0f)]
    [InlineData(0f, 127.5f)]
    [InlineData(1f, 255f)]
    public void RangeMatchesTheTriggerConvention(float input, float expected)
    {
        float actual = new RangeModifier(-1f, 1f, 0f, 255f).Apply(input);
        Assert.True(System.Math.Abs(expected - actual) < Tol, $"{input} -> {actual}, expected {expected}");
    }


    /**
     * A dead zone that merely thresholds makes the stick JUMP at the boundary. This one
     * rescales, so the response is continuous.
     */
    [Fact]
    public void DeadZoneIsContinuousAtTheThreshold()
    {
        var dz = new DeadZoneModifier(0.2f);

        Assert.Equal(0f, dz.Apply(0.1f));
        Assert.Equal(0f, dz.Apply(-0.2f));

        // just past the edge, output is near zero rather than jumping to 0.2
        Assert.True(dz.Apply(0.201f) < 0.01f);
        Assert.True(dz.Apply(1f) > 0.999f);
        Assert.True(dz.Apply(-1f) < -0.999f);
    }


    [Fact]
    public void InvertNegates()
    {
        Assert.Equal(-0.5f, new InvertModifier().Apply(0.5f));
    }


    [Fact]
    public void ModifiersRoundTripThroughTheirSpec()
    {
        foreach (var spec in new[] { "deadzone 0.15", "curve 4", "invert", "range -1 1 0 255" })
        {
            var m = InputModifiers.TryParse(spec);
            Assert.NotNull(m);
            Assert.Equal(spec, InputModifiers.ToSpec(m!));
        }

        Assert.Null(InputModifiers.TryParse("nosuchmodifier"));
        Assert.Null(InputModifiers.TryParse(""));
    }


    /**
     * Order is part of the meaning: a dead zone before a curve is not the same
     * transform as a curve before a dead zone.
     */
    [Fact]
    public void PipelineAppliesInOrder()
    {
        var deadThenCurve = new IInputModifier[] { new DeadZoneModifier(0.5f), new CurveModifier(2f) };
        var curveThenDead = new IInputModifier[] { new CurveModifier(2f), new DeadZoneModifier(0.5f) };

        // 0.6 survives the dead zone (rescaled to 0.2) then squares to 0.04
        float a = InputModifiers.Apply(deadThenCurve, 0.6f);
        // 0.6 squares to 0.36, which the dead zone then kills entirely
        float b = InputModifiers.Apply(curveThenDead, 0.6f);

        Assert.True(System.Math.Abs(a - 0.04f) < Tol, $"deadThenCurve = {a}");
        Assert.Equal(0f, b);
        Assert.NotEqual(a, b);
    }
}
