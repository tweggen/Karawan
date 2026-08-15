using System;
using System.Linq;
using System.Reflection;
using engine;
using Xunit;

namespace JoyceCode.Tests.engine;

/**
 * A debug category filter may hide TRACE. It must never hide a warning or an error.
 *
 * It did. `Logger` carried both `Warning(Dc, ref DebugInterpolatedStringHandler)` and
 * `Warning(Dc, in string)`, the second documented as "Always emits (not filtered)". An
 * interpolated string argument prefers the HANDLER overload, so all 57 `Warning(_dc,
 * $"...")` call sites in the tree bound to the silent one and printed nothing unless that
 * category happened to be switched on.
 *
 * The cost was not hypothetical. TaleSpawnOperator has long carried a post-spawn health
 * check that says "E-to-Talk will not work" - the exact symptom of a half-built NPC - and
 * it never printed once. Two rounds of investigation treated "there are no logs" as
 * evidence that nothing was wrong.
 *
 * This pins the shape rather than the behaviour, because the failure was a shape: the
 * existence of a filtered overload that wins overload resolution.
 */
public class LoggerFilteringTests
{
    private static MethodInfo[] _overloadsOf(string name)
        => typeof(Logger)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == name)
            .ToArray();


    private static bool _isFiltered(MethodInfo m)
        => m.GetParameters().Any(p =>
            p.ParameterType.Name.Contains("DebugInterpolatedStringHandler", StringComparison.Ordinal));


    /**
     * Trace is allowed - indeed required - to have the filtered form. That is what the
     * whole category system is for.
     */
    [Fact]
    public void TraceKeepsItsFilteredOverload()
    {
        Assert.Contains(_overloadsOf("Trace"), _isFiltered);
    }


    [Fact]
    public void NoWarningOverloadIsSilencedByACategory()
    {
        var filtered = _overloadsOf("Warning").Where(_isFiltered).ToList();

        Assert.True(filtered.Count == 0,
            "Logger.Warning has an overload taking DebugInterpolatedStringHandler. An "
            + "interpolated argument binds to it in preference to Warning(Dc, string), so "
            + "every Warning(_dc, $\"...\") in the tree becomes invisible unless that "
            + "category is enabled. A warning is not trace detail.");
    }


    [Fact]
    public void NoErrorOverloadIsSilencedByACategory()
    {
        var filtered = _overloadsOf("Error").Where(_isFiltered).ToList();

        Assert.True(filtered.Count == 0,
            "Logger.Error has an overload taking DebugInterpolatedStringHandler - see "
            + nameof(NoWarningOverloadIsSilencedByACategory) + ".");
    }


    /**
     * And the categorised forms must still exist, or the 57 call sites stop compiling
     * rather than start printing.
     */
    /*
     * `in string` reflects as System.String ByRef, so the element type is what has to be
     * compared. Learned by writing this assertion the obvious way and watching it fail.
     */
    private static bool _isCategorisedString(MethodInfo m)
    {
        var p = m.GetParameters();
        return p.Length == 2
               && p[0].ParameterType == typeof(Dc)
               && (p[1].ParameterType == typeof(string)
                   || p[1].ParameterType.GetElementType() == typeof(string));
    }


    [Fact]
    public void TheCategorisedFormsStillExist()
    {
        Assert.Contains(_overloadsOf("Warning"), _isCategorisedString);
        Assert.Contains(_overloadsOf("Error"), _isCategorisedString);
    }
}
