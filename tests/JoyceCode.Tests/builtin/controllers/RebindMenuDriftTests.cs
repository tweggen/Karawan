using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using builtin.controllers;
using builtin.controllers.bindings;
using engine.inputs;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.builtin.controllers;

/**
 * The rebinding screen calls into C# by NAME, from XML, through Lua (WP-6.4).
 *
 * `onClick='bindings:beginCapture(action)'` is a string. Rename the C# method and nothing
 * fails to compile, nothing fails to load, and nothing fails to render - the option just
 * stops doing anything when a player clicks it. That is the same shape as the 14 TALE
 * storylets that were loadable but undeclared, and as the flat binding table that was
 * emptied while `MapButtonToLogical` still existed: a declaration and its target drifting
 * apart with no build step between them.
 *
 * So: parse the menu, extract every `bindings:<name>` it invokes, and require the method
 * to exist.
 */
public class RebindMenuDriftTests
{
    private readonly ITestOutputHelper _output;

    public RebindMenuDriftTests(ITestOutputHelper output)
    {
        _output = output;
    }


    private static string? _findMenuXml()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, "models", "menu", "menu.xml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }


    [Fact]
    public void EveryLuaCallTheMenuMakesExistsOnTheBindingsClass()
    {
        string? menu = _findMenuXml();
        Assert.True(null != menu, "could not locate models/menu/menu.xml");

        string xml = File.ReadAllText(menu!);

        var invoked = new SortedSet<string>(
            Regex.Matches(xml, @"bindings:([A-Za-z_][A-Za-z0-9_]*)")
                .Select(m => m.Groups[1].Value));

        Assert.True(invoked.Count > 0,
            "menu.xml invokes nothing on 'bindings' - either the Controls screen is gone or "
            + "this test is looking at the wrong file");

        var available = typeof(BindingsLuaBindings)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToHashSet(StringComparer.Ordinal);

        var missing = invoked.Where(name => !available.Contains(name)).ToList();

        _output.WriteLine($"menu.xml calls: {string.Join(", ", invoked)}");

        Assert.True(missing.Count == 0,
            $"menu.xml calls bindings:{string.Join("/", missing)}, which "
            + $"{nameof(BindingsLuaBindings)} does not have. Lua resolves this by name at "
            + "click time, so nothing else would have noticed.");
    }


    /**
     * The Controls screen must be reachable. A view nothing navigates to is dead weight
     * that still looks present in the file.
     */
    [Fact]
    public void TheControlsScreenIsReachableFromTheMenu()
    {
        string? menu = _findMenuXml();
        Assert.True(null != menu, "could not locate models/menu/menu.xml");

        string xml = File.ReadAllText(menu!);

        Assert.Contains("id='menuControls'", xml);
        Assert.Contains("\"menuControls\"", xml);
    }
}


/**
 * The two-file design, which is the part of WP-6.4 that a rebinding UI finally exercises.
 *
 * Shipped defaults and the user's overrides are separate files layered per ACTION. The
 * promise is that a player's rebindings survive an update AND that bindings the update
 * adds still appear. Both halves are asserted here, because getting either wrong is
 * invisible until someone updates the game.
 */
public class UserOverlayTests
{
    private static BindingTable _shipped()
    {
        var t = new BindingTable();
        t.FromJsonString("""
            {
              "jump":     { "controls": [ "Key:Space", "GamepadButton:A" ] },
              "interact": { "controls": [ "Key:E" ] }
            }
            """);
        return t;
    }


    /**
     * What the Rebind button does, as opposed to what BindingTable.Bind does.
     *
     * Bind appends. That is right for a table where "interact" is E and Pad Y, and wrong
     * for a button labelled Rebind: a player who rebinds jump to J and finds Space still
     * jumping has added a key, not replaced one. The KEYBOARD binding is replaced and the
     * GAMEPAD one survives, so rebinding at the keyboard does not quietly cost them their
     * controller.
     */
    [Fact]
    public void RebindReplacesTheSameKindAndLeavesTheOthers()
    {
        var live = _shipped();

        RebindController.Rebind(live, "jump", Control.Key(ScanCode.J));

        Assert.Equal("jump", live.ActionOf(Control.Key(ScanCode.J)));
        Assert.Null(live.ActionOf(Control.Key(ScanCode.Space)));      // replaced
        Assert.Equal("jump", live.ActionOf(Control.GamepadButton("A"))); // untouched
    }


    /**
     * And the same when the captured control belongs to someone else: it moves, so one
     * press can never fire two actions.
     */
    [Fact]
    public void RebindStealsAControlFromItsPreviousAction()
    {
        var live = _shipped();

        RebindController.Rebind(live, "jump", Control.Key(ScanCode.E));

        Assert.Equal("jump", live.ActionOf(Control.Key(ScanCode.E)));
        Assert.Empty(live.Find("interact")!.Controls);
        Assert.Null(live.ActionOf(Control.Key(ScanCode.Space)));
    }


    /**
     * Escape is reserved as the way out, and it has to be, because capture swallows every
     * key and button - a gamepad player would otherwise have no route out of "press a
     * key" at all. The cost is stated rather than hidden: Escape cannot be assigned from
     * the screen.
     */
    [Fact]
    public void EscapeIsTheReservedCancelControl()
    {
        Assert.Equal(Control.Key(ScanCode.Escape), RebindController.CancelControl);
    }


    [Fact]
    public void AUserRebindSurvivesAnUpdateThatAddsActions()
    {
        /*
         * The player rebinds jump, and their file records only that.
         */
        var live = _shipped();
        RebindController.Rebind(live, "jump", Control.Key(ScanCode.J));

        var userFile = new BindingTable();
        userFile.FromJsonString(live.ToJsonString());

        /*
         * The update ships a new action and leaves the others alone.
         */
        var updated = new BindingTable();
        updated.FromJsonString("""
            {
              "jump":     { "controls": [ "Key:Space", "GamepadButton:A" ] },
              "interact": { "controls": [ "Key:E" ] },
              "holster":  { "controls": [ "Key:H" ] }
            }
            """);

        foreach (var binding in userFile.Actions)
        {
            updated.Set(binding);
        }

        // the rebind survived
        Assert.Equal("jump", updated.ActionOf(Control.Key(ScanCode.J)));
        Assert.Null(updated.ActionOf(Control.Key(ScanCode.Space)));

        // the action the update added is present, which a whole-file overlay would have masked
        Assert.Equal("holster", updated.ActionOf(Control.Key(ScanCode.H)));

        // and an action the player never touched still works
        Assert.Equal("interact", updated.ActionOf(Control.Key(ScanCode.E)));
    }


}
