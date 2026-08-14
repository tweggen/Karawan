using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using builtin.controllers.bindings;
using engine.inputs;
using Xunit;
using Xunit.Abstractions;

namespace JoyceCode.Tests.builtin.controllers;

/**
 * Drift tests for the SHIPPED binding file (WP-6.4).
 *
 * The unit tests above prove the layer works on data they construct themselves. These
 * prove the data the game actually ships is valid and complete - the distinction that
 * mattered when 14 TALE storylets were loadable but undeclared, and again when a bake
 * identity did not match what the runtime asked for.
 *
 * Mirrors tests/JoyceCode.Tests/engine/tale/TaleStoryletResourceTests.cs.
 */
public class ShippedBindingsTests
{
    private readonly ITestOutputHelper _output;

    public ShippedBindingsTests(ITestOutputHelper output)
    {
        _output = output;
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


    private static BindingTable? _load(out int nSkipped)
    {
        nSkipped = 0;
        string? models = _findModels();
        if (null == models)
        {
            return null;
        }

        var table = new BindingTable();
        nSkipped = table.FromJsonString(File.ReadAllText(Path.Combine(models, "nogame.bindings.json")));
        return table;
    }


    [Fact]
    public void ShippedBindingsParseWithNothingSkipped()
    {
        var table = _load(out int nSkipped);
        Assert.NotNull(table);

        Assert.Equal(0, nSkipped);
        Assert.NotEmpty(table!.Actions);
        _output.WriteLine($"{table.Actions.Count} actions, "
                          + $"{table.Actions.Sum(a => a.Controls.Count)} controls");
    }


    /**
     * Every action the flat MapButtonToLogical table used to provide must still be
     * bound. This is the check that the migration was COMPLETE: the old entries were
     * deleted from nogame.implementations.json, so anything missed here is a binding
     * the player silently lost.
     */
    [Theory]
    [InlineData("interact")]
    [InlineData("change")]
    [InlineData("menu")]
    [InlineData("map")]
    [InlineData("jump")]
    [InlineData("run")]
    [InlineData("followquest")]
    [InlineData("cursorup")]
    [InlineData("cursordown")]
    [InlineData("cursorleft")]
    [InlineData("cursorright")]
    public void EveryMigratedActionIsStillBound(string action)
    {
        var table = _load(out _);
        Assert.NotNull(table);

        var binding = table!.Find(action);
        Assert.True(null != binding, $"action '{action}' is not in nogame.bindings.json");
        Assert.NotEmpty(binding!.Controls);
    }


    /**
     * The keyboard controls the old table carried, by position. Spot-checking the ones
     * whose ScanCode name differs from the old engine key string is the point: "(escape)"
     * became Escape, "(shiftleft)" became LeftShift, and " " became Space - each an
     * opportunity to have transcribed the wrong key.
     */
    [Theory]
    [InlineData("interact", ScanCode.E)]
    [InlineData("change", ScanCode.F)]
    [InlineData("menu", ScanCode.Escape)]
    [InlineData("map", ScanCode.Tab)]
    [InlineData("jump", ScanCode.Space)]
    [InlineData("run", ScanCode.LeftShift)]
    [InlineData("followquest", ScanCode.Q)]
    public void MigratedKeysKeptTheirPhysicalPosition(string action, ScanCode expected)
    {
        var table = _load(out _);
        Assert.NotNull(table);

        Assert.Equal(action, table!.ActionOf(Control.Key(expected)));
    }


    [Theory]
    [InlineData("interact", "Y")]
    [InlineData("change", "X")]
    [InlineData("menu", "Start")]
    [InlineData("map", "Back")]
    [InlineData("jump", "A")]
    [InlineData("run", "LeftShoulder")]
    [InlineData("cursorup", "DPadUp")]
    [InlineData("cursordown", "DPadDown")]
    [InlineData("cursorleft", "DPadLeft")]
    [InlineData("cursorright", "DPadRight")]
    public void MigratedGamepadButtonsSurvived(string action, string button)
    {
        var table = _load(out _);
        Assert.NotNull(table);

        Assert.Equal(action, table!.ActionOf(Control.GamepadButton(button)));
    }


    /**
     * No control may drive two actions. The table's reverse index silently lets the
     * last one win, so a duplicate in the file would be a binding that mysteriously
     * does the wrong thing.
     */
    [Fact]
    public void NoControlIsBoundTwice()
    {
        var table = _load(out _);
        Assert.NotNull(table);

        var seen = new Dictionary<Control, string>();
        var duplicates = new List<string>();

        foreach (var binding in table!.Actions)
        {
            foreach (var control in binding.Controls)
            {
                if (seen.TryGetValue(control, out var other))
                {
                    duplicates.Add($"{control} drives both '{other}' and '{binding.Action}'");
                }
                else
                {
                    seen[control] = binding.Action;
                }
            }
        }

        Assert.True(duplicates.Count == 0, string.Join("; ", duplicates));
    }


    /**
     * The shipped file must be declared as a resource, or it will not be in the APK -
     * and the game would start with no button bindings. This is the TALE storylet
     * failure exactly, and it is cheap to prevent.
     */
    [Fact]
    public void ShippedBindingsAreDeclaredAsAResource()
    {
        string? models = _findModels();
        Assert.NotNull(models);

        string resources = Path.Combine(models!, "nogame.resources.json");
        Assert.True(File.Exists(resources));

        Assert.Contains("nogame.bindings.json", File.ReadAllText(resources));
    }
}
