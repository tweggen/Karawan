using System.Numerics;
using Xunit;

/*
 * Aliased, not `using builtin.tools;`. This file's own namespace declares
 * JoyceCode.Tests.builtin, so an unqualified `builtin.tools` binds to THIS namespace and the
 * type is not found. The global:: qualifier is what makes it unambiguous while keeping the
 * namespace matching the source path, as the rest of the suite does.
 */
using SafeOrientation = global::builtin.tools.SafeOrientation;

namespace JoyceCode.Tests.builtin.tools;

/**
 * Regression tests for the boot loop of 2026-08-08: a persisted player orientation that was
 * not a unit quaternion aborted the process inside BepuPhysics.Bodies.Add, on every launch.
 */
public class SafeOrientationTests
{
    /**
     * The assumption that caused the bug. Normalize is NOT a validation step: on the all-zero
     * default it computes 1/sqrt(0) = Infinity, then 0 * Infinity = NaN. The call site read as
     * if it guaranteed a unit quaternion, and it guaranteed nothing.
     */
    [Fact]
    public void QuaternionNormalize_DoesNotRescue_TheZeroQuaternion()
    {
        Quaternion normalized = Quaternion.Normalize(default);

        Assert.True(float.IsNaN(normalized.X));
        Assert.True(float.IsNaN(normalized.W));
    }


    /**
     * And default(Quaternion) is not identity, which is what makes an unassigned field or a
     * deserialisation that missed one land straight in the case above.
     */
    [Fact]
    public void DefaultQuaternion_IsNotIdentity()
    {
        Assert.NotEqual(Quaternion.Identity, default);
        Assert.Equal(0f, default(Quaternion).Length());
    }


    [Fact]
    public void Sanitize_ReplacesZeroQuaternion_WithIdentity()
    {
        Quaternion result = SafeOrientation.Sanitize(default, out bool wasRepaired);

        Assert.True(wasRepaired);
        Assert.Equal(Quaternion.Identity, result);
    }


    [Theory]
    [InlineData(float.NaN, 0f, 0f, 0f)]
    [InlineData(0f, float.PositiveInfinity, 0f, 0f)]
    [InlineData(0f, 0f, float.NegativeInfinity, 0f)]
    [InlineData(float.NaN, float.NaN, float.NaN, float.NaN)]
    public void Sanitize_ReplacesNonFinite_WithIdentity(float x, float y, float z, float w)
    {
        Quaternion result = SafeOrientation.Sanitize(new Quaternion(x, y, z, w), out bool wasRepaired);

        Assert.True(wasRepaired);
        Assert.Equal(Quaternion.Identity, result);
    }


    [Fact]
    public void Sanitize_PassesThrough_AnAlreadyUnitQuaternion()
    {
        Quaternion q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.1f);

        Quaternion result = SafeOrientation.Sanitize(q, out bool wasRepaired);

        Assert.False(wasRepaired);
        Assert.Equal(q, result);
    }


    /**
     * Drift off the unit sphere is what repeated incremental rotation produces. That is a real
     * rotation and must be rescaled, not discarded - replacing it with identity would snap the
     * player to a new heading.
     */
    [Fact]
    public void Sanitize_RescalesADriftedQuaternion_RatherThanDiscardingIt()
    {
        Quaternion q = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.1f) * 1.5f;

        Quaternion result = SafeOrientation.Sanitize(q, out bool wasRepaired);

        Assert.True(wasRepaired);
        Assert.Equal(1f, result.Length(), 4);
        Assert.NotEqual(Quaternion.Identity, result);
    }


    /**
     * Whatever comes out must satisfy the condition BepuPhysics asserts on, for every input.
     */
    [Theory]
    [InlineData(0f, 0f, 0f, 0f)]
    [InlineData(float.NaN, 1f, 0f, 0f)]
    [InlineData(0f, 0f, 0f, 1f)]
    [InlineData(3f, 4f, 0f, 0f)]
    [InlineData(1e-9f, 0f, 0f, 0f)]
    public void Sanitize_AlwaysYieldsAUnitQuaternion(float x, float y, float z, float w)
    {
        Quaternion result = SafeOrientation.Sanitize(new Quaternion(x, y, z, w), out _);

        Assert.Equal(1f, result.Length(), 3);
    }
}
