using System;
using System.Collections.Generic;
using System.Globalization;

namespace builtin.controllers.bindings;

/**
 * A transform applied to an analog control's value on its way to an action (WP-6.4).
 *
 * These are not speculative. Every modifier here exists because the behaviour is
 * ALREADY in the codebase, hardcoded at a call site where it cannot be seen, changed or
 * tested:
 *
 *   Curve      InputController.StickTransfer is sign(x) * |x^4| - a response curve
 *              written as an expression in the middle of the stick handler.
 *   Range      _onTriggerMoved does (x + 1) / 2 * 255 to turn SDL's -1..1 trigger into
 *              the 0..255 the controller state wants. That is a range remap, and the
 *              ledger records that this convention was one of three judgement calls
 *              WP-3.1 flagged for GATE-C precisely because nobody could see it.
 *   Invert     WP-3.1 negated a stick axis based on a FIELD NAME and got it backwards;
 *              #61 fixed it. A named, testable inversion is what that should have been.
 *   DeadZone   not currently implemented anywhere, which is itself the observation.
 *
 * Turning them into data is the point of the work package: "those are modifiers in
 * disguise and should become data."
 *
 * Order matters and is the order given. Unreal evaluates modifiers as a pipeline and so
 * does this; a dead zone applied after a curve is a different thing from one applied
 * before it.
 */
public interface IInputModifier
{
    /**
     * The persisted name, e.g. "deadzone". Lowercase, stable - it is JSON.
     */
    string Name { get; }

    float Apply(float value);

    /**
     * The persisted parameters, in the form the factory accepts back.
     */
    IReadOnlyList<float> Parameters { get; }
}


/**
 * Zero out small magnitudes, then rescale the remainder to the full range so the
 * response is continuous at the threshold rather than jumping.
 */
public sealed class DeadZoneModifier : IInputModifier
{
    private readonly float _threshold;

    public DeadZoneModifier(float threshold)
    {
        _threshold = Math.Clamp(threshold, 0f, 0.99f);
    }

    public string Name => "deadzone";

    public IReadOnlyList<float> Parameters => new[] { _threshold };

    public float Apply(float value)
    {
        float magnitude = Math.Abs(value);
        if (magnitude <= _threshold)
        {
            return 0f;
        }

        /*
         * Rescaled, not merely thresholded. Without this the stick jumps from 0 to
         * _threshold the instant it passes the boundary.
         */
        float scaled = (magnitude - _threshold) / (1f - _threshold);
        return Math.Sign(value) * Math.Min(scaled, 1f);
    }
}


/**
 * sign(x) * |x|^exponent. Exponent 4 reproduces InputController.StickTransfer exactly.
 */
public sealed class CurveModifier : IInputModifier
{
    private readonly float _exponent;

    public CurveModifier(float exponent)
    {
        _exponent = exponent;
    }

    public string Name => "curve";

    public IReadOnlyList<float> Parameters => new[] { _exponent };

    public float Apply(float value)
        => Math.Sign(value) * MathF.Pow(Math.Abs(value), _exponent);
}


public sealed class InvertModifier : IInputModifier
{
    public string Name => "invert";

    public IReadOnlyList<float> Parameters => Array.Empty<float>();

    public float Apply(float value) => -value;
}


/**
 * Linear remap from one range onto another. Reproduces the trigger convention
 * (-1..1 -> 0..255) as data instead of arithmetic at the call site.
 */
public sealed class RangeModifier : IInputModifier
{
    private readonly float _inMin, _inMax, _outMin, _outMax;

    public RangeModifier(float inMin, float inMax, float outMin, float outMax)
    {
        _inMin = inMin;
        _inMax = inMax;
        _outMin = outMin;
        _outMax = outMax;
    }

    public string Name => "range";

    public IReadOnlyList<float> Parameters => new[] { _inMin, _inMax, _outMin, _outMax };

    public float Apply(float value)
    {
        float span = _inMax - _inMin;
        if (Math.Abs(span) < 1e-9f)
        {
            return _outMin;
        }

        float t = (value - _inMin) / span;
        return _outMin + t * (_outMax - _outMin);
    }
}


public static class InputModifiers
{
    /**
     * Build a modifier from its persisted form: the name, then whitespace-separated
     * parameters, e.g. "deadzone 0.15", "curve 4", "range -1 1 0 255", "invert".
     *
     * A single string rather than a nested object because these live inside a JSON
     * binding table a human edits by hand, and `"modifiers": ["deadzone 0.15", "curve 4"]`
     * reads as a pipeline, which is what it is.
     */
    public static IInputModifier? TryParse(string? spec)
    {
        if (string.IsNullOrWhiteSpace(spec))
        {
            return null;
        }

        string[] parts = spec.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string name = parts[0].ToLowerInvariant();

        float Arg(int i, float fallback)
            => parts.Length > i + 1
               && float.TryParse(parts[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var f)
                ? f
                : fallback;

        switch (name)
        {
            case "deadzone":
                return new DeadZoneModifier(Arg(0, 0.1f));
            case "curve":
                return new CurveModifier(Arg(0, 1f));
            case "invert":
                return new InvertModifier();
            case "range":
                return new RangeModifier(Arg(0, -1f), Arg(1, 1f), Arg(2, 0f), Arg(3, 1f));
            default:
                return null;
        }
    }


    public static string ToSpec(IInputModifier modifier)
    {
        if (modifier.Parameters.Count == 0)
        {
            return modifier.Name;
        }

        var parts = new List<string>(modifier.Parameters.Count + 1) { modifier.Name };
        foreach (var p in modifier.Parameters)
        {
            parts.Add(p.ToString("R", CultureInfo.InvariantCulture));
        }

        return string.Join(' ', parts);
    }


    /**
     * Run the pipeline in order.
     */
    public static float Apply(IReadOnlyList<IInputModifier>? modifiers, float value)
    {
        if (null == modifiers)
        {
            return value;
        }

        for (int i = 0; i < modifiers.Count; ++i)
        {
            value = modifiers[i].Apply(value);
        }

        return value;
    }
}
