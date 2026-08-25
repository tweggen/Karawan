using System.Collections.Generic;
using builtin.controllers.bindings;
using engine.inputs;
using Xunit;

namespace JoyceCode.Tests.builtin.controllers;

/**
 * Labels for the rebinding screen (WP-6.4).
 *
 * These run with no engine, which is the interesting configuration: it is the fallback
 * path, and the fallback is what a headless host and any platform that cannot answer will
 * actually render. A label that only looks right when SDL is present is a label nobody
 * checked.
 */
public class ControlLabelTests
{
    /**
     * The fallback is the ScanCode's ENUM name, not ScanCodeNames' engine code.
     *
     * ScanCodeNames would give " " for Space and "(escape)" for Escape. A cell containing
     * a single space is indistinguishable from an unbound action, and "(escape)" is a wire
     * format leaking into a UI.
     */
    [Theory]
    [InlineData(ScanCode.W, "W")]
    [InlineData(ScanCode.Space, "Space")]
    [InlineData(ScanCode.Escape, "Escape")]
    [InlineData(ScanCode.LeftShift, "LeftShift")]
    public void KeysFallBackToTheScanCodeName(ScanCode scanCode, string expected)
    {
        Assert.Equal(expected, ControlLabels.Of(Control.Key(scanCode), null));
    }


    /**
     * Gamepad controls are prefixed so a mixed row reads unambiguously. "E / Pad Y" needs
     * no legend; "E / Y" invites the reader to think Y is a letter key.
     */
    [Theory]
    [InlineData("Y", "Pad Y")]
    [InlineData("DPadUp", "Pad DPadUp")]
    public void GamepadButtonsArePrefixed(string button, string expected)
    {
        Assert.Equal(expected, ControlLabels.Of(Control.GamepadButton(button), null));
    }


    [Fact]
    public void AnalogControlsGetTheirConventionalNames()
    {
        Assert.Equal("Pad LT", ControlLabels.Of(Control.GamepadTrigger(0), null));
        Assert.Equal("Pad RT", ControlLabels.Of(Control.GamepadTrigger(1), null));
        Assert.Equal("Pad L-Stick", ControlLabels.Of(Control.GamepadStick(0), null));
        Assert.Equal("Pad R-Stick", ControlLabels.Of(Control.GamepadStick(1), null));
    }


    [Fact]
    public void MouseButtonsUseTheUsualAbbreviations()
    {
        Assert.Equal("LMB", ControlLabels.Of(Control.MouseButton(0), null));
        Assert.Equal("RMB", ControlLabels.Of(Control.MouseButton(1), null));
        Assert.Equal("MMB", ControlLabels.Of(Control.MouseButton(2), null));
    }


    [Fact]
    public void SeveralControlsReadAsOneCell()
    {
        var controls = new List<Control> { Control.Key(ScanCode.E), Control.GamepadButton("Y") };
        Assert.Equal("E / Pad Y", ControlLabels.Of(controls, null));
    }


    /**
     * An unbound action must SAY it is unbound. An empty cell is indistinguishable from a
     * rendering fault, and this is the one row a player most needs to notice - it is what
     * they see after unbinding something by accident.
     */
    [Fact]
    public void NothingBoundSaysSo()
    {
        Assert.Equal("unbound", ControlLabels.Of(new List<Control>(), null));
        Assert.Equal("unbound", ControlLabels.Of(null!, null));
    }


    /**
     * Control.None has no label to give. It should still render as something.
     */
    [Fact]
    public void TheEmptyControlRendersAsADash()
    {
        Assert.Equal("-", ControlLabels.Of(Control.None, null));
    }
}
