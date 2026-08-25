using System.Numerics;
using System.Reflection;
using builtin.controllers;
using engine.news;
using Xunit;

namespace JoyceCode.Tests.builtin.controllers;

/**
 * Mouse-look must consume the platform's RELATIVE motion, never a difference of two
 * absolute pointer positions (KI-19).
 *
 * The absolute position is clamped to the window, and while the cursor is hidden - which
 * is the normal gameplay state, because that is what turns on relative-mouse mode - it is
 * warped as well. So differencing it produces motion right up until the pointer reaches a
 * border and then silently produces nothing. That does not fail, does not log, and does
 * not look like an input bug: the camera simply stops turning, which reads as a deliberate
 * rotation limit. It survived the whole Silk exit because Silk's CursorMode.Raw reported
 * an unbounded position and SDL3 does not.
 *
 * The test therefore pins the position and moves only the delta, which is exactly the
 * situation at the border.
 */
public class MouseLookRelativeMotionTests
{
    /**
     * _desktopMouseController is what OnLogicalFrame calls; driving it directly keeps the
     * test off the module lifecycle, which needs the whole DI graph.
     */
    private static void _runFrame(InputController ic)
    {
        var mi = typeof(InputController).GetMethod(
            "_desktopMouseController", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.True(null != mi,
            "InputController._desktopMouseController is gone - if mouse-look moved elsewhere, "
            + "move this test with it rather than deleting it");
        mi!.Invoke(ic, null);
    }


    private static Event _moved(Vector2 position, Vector2 delta)
        => new Event(Event.INPUT_MOUSE_MOVED, "")
        {
            PhysicalPosition = position,
            PhysicalDelta = delta
        };


    private static InputController _newController()
    {
        /*
         * _handleMouseMoved and the desktop controller are both behind this check.
         */
        global::engine.GlobalSettings.Set("splash.touchControls", "false");
        return new InputController() { MouseLookMoveSensitivity = 1f };
    }


    [Fact]
    public void MotionAtAPinnedPositionStillTurnsTheCamera()
    {
        var ic = _newController();

        /*
         * The pointer is stuck against the right edge of the window: SDL keeps reporting
         * the same x, while xrel keeps reporting real movement.
         */
        var atBorder = new Vector2(1919f, 540f);
        for (int i = 0; i < 4; ++i)
        {
            ic.InputPartOnInputEvent(_moved(atBorder, new Vector2(10f, 0f)));
        }

        _runFrame(ic);
        ic.GetMouseMove(out var v2Move);

        Assert.Equal(40f, v2Move.X, 3);
        Assert.Equal(0f, v2Move.Y, 3);
    }


    [Fact]
    public void DeltasAccumulateAcrossEventsAndAreConsumedOnce()
    {
        var ic = _newController();

        ic.InputPartOnInputEvent(_moved(new Vector2(100f, 100f), new Vector2(3f, -2f)));
        ic.InputPartOnInputEvent(_moved(new Vector2(103f, 98f), new Vector2(1f, 5f)));

        _runFrame(ic);
        ic.GetMouseMove(out var v2First);
        Assert.Equal(new Vector2(4f, 3f), v2First);

        /*
         * Nothing moved since, so the next frame must contribute nothing. A controller
         * that re-derived the offset from the last known position would repeat it.
         */
        _runFrame(ic);
        ic.GetMouseMove(out var v2Second);
        Assert.Equal(Vector2.Zero, v2Second);
    }


    [Fact]
    public void SensitivityScalesTheDelta()
    {
        var ic = _newController();
        ic.MouseLookMoveSensitivity = 2.5f;

        ic.InputPartOnInputEvent(_moved(new Vector2(10f, 10f), new Vector2(4f, 8f)));

        _runFrame(ic);
        ic.GetMouseMove(out var v2Move);

        Assert.Equal(new Vector2(10f, 20f), v2Move);
    }
}
