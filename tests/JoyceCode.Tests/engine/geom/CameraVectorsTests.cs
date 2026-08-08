using System.Numerics;
using engine.geom;
using Xunit;

namespace JoyceCode.Tests.engine.geom;

/**
 * Regression tests for the black screen of 2026-08-08.
 *
 * Chain: a saved player orientation of (0,0,0,0) -> Vector3.Transform(-UnitZ, q) returns the ZERO
 * vector (Transform does not require a unit quaternion, it scales by |q|^2) -> the front vector is
 * zero -> CreateVectorsFromPlaneFront divided by its length -> NaN -> FollowCameraController built
 * the camera position from it -> everything downstream NaN, black screen, and OpenAL logging
 * "Listener orientation out of range" every frame.
 */
public class CameraVectorsTests
{
    /**
     * The step that made an uninitialised orientation invisible until it was too late. This is
     * language behaviour, pinned so nobody "simplifies" the guard away on the assumption that a
     * quaternion reaching Transform must be a rotation.
     */
    [Fact]
    public void Transform_ByTheZeroQuaternion_YieldsTheZeroVector()
    {
        Vector3 vFront = Vector3.Transform(new Vector3(0f, 0f, -1f), default(Quaternion));

        Assert.Equal(Vector3.Zero, vFront);
    }


    [Fact]
    public void CreateVectorsFromPlaneFront_OnAZeroFront_DoesNotProduceNaN()
    {
        Camera.CreateVectorsFromPlaneFront(Vector3.Zero, out Vector3 vuFront, out Vector3 vuRight);

        Assert.All(new[] { vuFront.X, vuFront.Y, vuFront.Z, vuRight.X, vuRight.Y, vuRight.Z },
            component => Assert.True(float.IsFinite(component)));
        Assert.True(vuFront.Length() > 0.5f);
    }


    [Theory]
    [InlineData(float.NaN, 0f, 0f)]
    [InlineData(0f, float.PositiveInfinity, 0f)]
    [InlineData(float.NaN, float.NaN, float.NaN)]
    public void CreateVectorsFromPlaneFront_OnANonFiniteFront_DoesNotProduceNaN(float x, float y, float z)
    {
        Camera.CreateVectorsFromPlaneFront(new Vector3(x, y, z), out Vector3 vuFront, out Vector3 vuRight);

        Assert.All(new[] { vuFront.X, vuFront.Y, vuFront.Z, vuRight.X, vuRight.Y, vuRight.Z },
            component => Assert.True(float.IsFinite(component)));
    }


    /**
     * Straight up and straight down: Cross(front, up) collapses to zero, which is not NaN but is
     * an equally unusable basis for the caller to build a coordinate system from.
     */
    [Theory]
    [InlineData(0f, 1f, 0f)]
    [InlineData(0f, -1f, 0f)]
    public void CreateVectorsFromPlaneFront_OnAVerticalFront_StillReturnsAUsableBasis(float x, float y, float z)
    {
        Camera.CreateVectorsFromPlaneFront(new Vector3(x, y, z), out Vector3 vuFront, out Vector3 vuRight);

        Assert.True(vuFront.Length() > 0.5f);
        Assert.True(vuRight.Length() > 0.5f);
    }


    /**
     * The ordinary case must be bit-for-bit what it was before the guards - they may only fire on
     * degenerate input. Compared against the original formula rather than against a hand-written
     * expectation, so this stays a true "unchanged" assertion.
     *
     * Note the outputs are NOT unit vectors and never were: vuFront is the front flattened into
     * the horizontal plane, so its length is cos(elevation) - about 0.995 for this input. Asserting
     * unit length here would be asserting a property the function has never had.
     */
    [Theory]
    [InlineData(0.3f, 0.1f, -1f)]
    [InlineData(-2f, 0f, 0.5f)]
    [InlineData(1f, 0.9f, 1f)]
    public void CreateVectorsFromPlaneFront_OnAnOrdinaryFront_IsUnchanged(float x, float y, float z)
    {
        Vector3 vFront = new(x, y, z);
        Vector3 vuUp = new(0f, 1f, 0f);

        // The original implementation, verbatim.
        Vector3 vuTmpExpected = vFront / vFront.Length();
        Vector3 vuRightExpected = Vector3.Cross(vuTmpExpected, vuUp);
        Vector3 vuFrontExpected = -Vector3.Cross(vuRightExpected, vuUp);

        Camera.CreateVectorsFromPlaneFront(vFront, out Vector3 vuFront, out Vector3 vuRight);

        Assert.Equal(vuFrontExpected, vuFront);
        Assert.Equal(vuRightExpected, vuRight);

        // And the flattening still holds: no vertical component survives in the front.
        Assert.Equal(0f, vuFront.Y, 5);
    }
}
