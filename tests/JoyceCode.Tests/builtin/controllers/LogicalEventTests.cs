using System;
using System.IO;
using builtin.controllers;
using builtin.controllers.bindings;
using engine.inputs;
using engine.news;
using Xunit;

namespace JoyceCode.Tests.builtin.controllers;

/**
 * End-to-end over the SHIPPED bindings: a raw platform event in, the logical event code
 * game code actually switches on out.
 *
 * WP-6.4's tests stopped at BindingTable.ActionOf, which answers "which action" - not
 * "which event string reaches WalkController". Those are one angle-bracket apart and the
 * brackets are added in InputMapper, so nothing pinned the join. This closes it: the
 * expectations below are transcribed from the switch statements that consume them
 * (WalkController.InputPartOnInputEvent, Widget.cs, Narration.cs), not from the binding
 * file, so a rename on either side fails here.
 */
public class LogicalEventTests
{
    private static InputMapper _mapper()
    {
        string? models = _findModels();
        Assert.True(null != models, "could not locate models/nogame.bindings.json");

        var mapper = new InputMapper();
        int nSkipped = mapper.Bindings.FromJsonString(
            File.ReadAllText(Path.Combine(models!, "nogame.bindings.json")));
        Assert.Equal(0, nSkipped);
        return mapper;
    }


    private static string? _findModels()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "models");
            if (File.Exists(Path.Combine(candidate, "nogame.bindings.json")))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }


    /**
     * The keyboard half. Codes are what the consumers switch on:
     * "<jump>" is WalkController, "<menu>"/"<map>" are the OSD, "<interact>" is Widget.
     */
    [Theory]
    [InlineData(ScanCode.Space, "<jump>")]
    [InlineData(ScanCode.LeftShift, "<run>")]
    [InlineData(ScanCode.E, "<interact>")]
    [InlineData(ScanCode.F, "<change>")]
    [InlineData(ScanCode.Escape, "<menu>")]
    [InlineData(ScanCode.Tab, "<map>")]
    [InlineData(ScanCode.Q, "<followquest>")]
    public void KeysProduceTheLogicalButtonGameCodeSwitchesOn(ScanCode scanCode, string expected)
    {
        var mapper = _mapper();

        var evPressed = new Event(Event.INPUT_KEY_PRESSED, ScanCodeNames.ToEngineCode(scanCode) ?? "")
        {
            ScanCode = scanCode
        };
        var logicalPressed = mapper.ToLogical(evPressed);

        Assert.True(null != logicalPressed, $"{scanCode} produced no logical event");
        Assert.Equal(Event.INPUT_BUTTON_PRESSED, logicalPressed!.Type);
        Assert.Equal(expected, logicalPressed.Code);

        /*
         * The release matters as much as the press: <run> and <fire> are HELD states in
         * WalkController, so a press with no matching release leaves the player running
         * forever.
         */
        var evReleased = new Event(Event.INPUT_KEY_RELEASED, ScanCodeNames.ToEngineCode(scanCode) ?? "")
        {
            ScanCode = scanCode
        };
        var logicalReleased = mapper.ToLogical(evReleased);

        Assert.True(null != logicalReleased, $"{scanCode} produced no logical release event");
        Assert.Equal(Event.INPUT_BUTTON_RELEASED, logicalReleased!.Type);
        Assert.Equal(expected, logicalReleased.Code);
    }


    [Theory]
    [InlineData("A", "<jump>")]
    [InlineData("LeftShoulder", "<run>")]
    [InlineData("Y", "<interact>")]
    [InlineData("X", "<change>")]
    [InlineData("Start", "<menu>")]
    [InlineData("Back", "<map>")]
    [InlineData("DPadUp", "<cursorup>")]
    [InlineData("DPadDown", "<cursordown>")]
    [InlineData("DPadLeft", "<cursorleft>")]
    [InlineData("DPadRight", "<cursorright>")]
    public void GamepadButtonsProduceTheLogicalButtonGameCodeSwitchesOn(string button, string expected)
    {
        var mapper = _mapper();

        var ev = new Event(Event.INPUT_GAMEPAD_BUTTON_PRESSED, button);
        var logical = mapper.ToLogical(ev);

        Assert.True(null != logical, $"gamepad {button} produced no logical event");
        Assert.Equal(Event.INPUT_BUTTON_PRESSED, logical!.Type);
        Assert.Equal(expected, logical.Code);
    }


    /**
     * A key event that never reached the ScanCode channel cannot be translated - the flat
     * table it used to fall through to is empty in the shipped config. Worth pinning
     * because the failure is silent: the event is simply dropped.
     */
    [Fact]
    public void AKeyEventWithoutAScanCodeTranslatesToNothing()
    {
        var mapper = _mapper();

        Assert.Null(mapper.ToLogical(new Event(Event.INPUT_KEY_PRESSED, " ")));
    }
}
