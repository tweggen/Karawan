using System.IO;
using System.Reflection;
using builtin.controllers;
using engine.news;
using Xunit;

namespace JoyceCode.Tests.builtin.controllers;

/**
 * The on-screen drive buttons (HoverTouchButton: accelerate, brake, left, right) must move
 * InputController's state.
 *
 * Before this test they pushed a synthetic INPUT_KEY_PRESSED "w"/"s"/"a"/"d". WP-6.4 moved
 * InputController onto ev.ScanCode, which a synthetic key event does not carry, so every
 * press was dropped without a log line: the car sat still on Android while "<change>" -
 * already a logical button - kept working. A touch button is not a keyboard key; it now
 * names the action (Event.INPUT_ACTION_PRESSED), and the event below is the one
 * HoverTouchButton builds.
 */
public class TouchDriveButtonTests
{
    private static InputController _newController()
    {
        var ic = new InputController();

        var fi = typeof(InputController).GetField("_bindings", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.True(null != fi, "InputController._bindings is gone - move this test with the bindings");
        var mapper = new InputMapper();
        mapper.Bindings.FromJsonString(File.ReadAllText(Path.Combine(_findModels(), "nogame.bindings.json")));
        fi!.SetValue(ic, mapper.Bindings);
        return ic;
    }


    private static string _findModels()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "models");
            if (File.Exists(Path.Combine(candidate, "nogame.bindings.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        Assert.Fail("could not locate models/nogame.bindings.json");
        return "";
    }


    private static ControllerState _state(InputController ic)
    {
        ic.GetControllerState(out var cs);
        return cs;
    }


    private static Event _touch(string action, bool isPressed)
        => new Event(isPressed ? Event.INPUT_ACTION_PRESSED : Event.INPUT_ACTION_RELEASED, action);


    /**
     * The event the buttons pushed until this fix. Kept as the reproduction: it must stay
     * inert, because a key event without a ScanCode means nothing to the bindings.
     */
    [Fact]
    public void ASyntheticKeyWithoutScanCodeDrivesNothing()
    {
        var ic = _newController();
        ic.InputPartOnInputEvent(new Event(Event.INPUT_KEY_PRESSED, "w"));
        Assert.Equal(0, _state(ic).WASDUp);
    }


    [Theory]
    [InlineData("walkforward")]
    [InlineData("walkbackward")]
    [InlineData("walkleft")]
    [InlineData("walkright")]
    public void TouchButtonPressDrivesAndReleaseStops(string action)
    {
        var ic = _newController();

        ic.InputPartOnInputEvent(_touch(action, true));
        var pressed = _state(ic);
        int value = action switch
        {
            "walkforward" => pressed.WASDUp,
            "walkbackward" => pressed.WASDDown,
            "walkleft" => pressed.WASDLeft,
            _ => pressed.WASDRight
        };
        Assert.True(value > 0, $"pressing the {action} touch button left the controller state at rest");

        ic.InputPartOnInputEvent(_touch(action, false));
        var released = _state(ic);
        Assert.Equal(0, released.WASDUp + released.WASDDown + released.WASDLeft + released.WASDRight);
    }


    /**
     * The keyboard path is untouched: W resolves through its ScanCode, and its logical twin
     * "<walkforward>" - which Platform pushes alongside - must NOT act, or a menu that
     * consumes the raw W for navigation would still move the car behind it.
     */
    [Fact]
    public void KeyboardWDrivesThroughItsScanCodeOnly()
    {
        var ic = _newController();
        var mapper = new InputMapper();
        mapper.Bindings.FromJsonString(File.ReadAllText(Path.Combine(_findModels(), "nogame.bindings.json")));

        var raw = new Event(Event.INPUT_KEY_PRESSED, "w") { ScanCode = global::engine.inputs.ScanCode.W };
        var logical = mapper.ToLogical(raw);
        Assert.NotNull(logical);
        ic.InputPartOnInputEvent(logical!);
        Assert.Equal(0, _state(ic).WASDUp);

        ic.InputPartOnInputEvent(raw);
        Assert.Equal(ic.KeyboardAnalogWalk, _state(ic).WASDUp);
    }


    /**
     * Every action a touch button names must be one InputController reads; a typo there
     * would be the same silent dead button.
     */
    [Theory]
    [InlineData("walkforward")]
    [InlineData("walkbackward")]
    [InlineData("walkleft")]
    [InlineData("walkright")]
    public void TouchActionsAreActionsTheControllerReads(string action)
    {
        Assert.Contains(action, InputController.RequiredActions);
    }
}
