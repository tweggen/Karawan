using System;
using System.Numerics;
using static engine.Logger;

namespace engine.geom;

public static class Camera
{
    static private Vector3 _vuUp = new(0f, 1f, 0f);

    /**
     * A front vector shorter than this carries no usable direction. Normalising it would
     * amplify floating point noise into an arbitrary heading, and at exactly zero it produces
     * NaN outright.
     */
    private const float MinFrontLength = 1e-5f;

    /**
     * Fallback heading, matching the -Z convention every caller uses for "forward".
     */
    private static readonly Vector3 _vuFrontDefault = new(0f, 0f, -1f);


    /**
     * Derive an orthonormal front/right pair from an arbitrary front vector.
     *
     * THE DIVISION BELOW USED TO BE UNGUARDED, and it is the shortest path from a bad
     * orientation to a black screen:
     *
     *     var vuTmpFront = vFront / vFront.Length();     // 0/0 = NaN
     *
     * Callers derive vFront with Vector3.Transform(-UnitZ, qFront), and Vector3.Transform does
     * NOT require a unit quaternion - it scales the result by |q|^2. So a qFront of (0,0,0,0),
     * which is what default(Quaternion) is and what an orientation that was never initialised
     * looks like, yields a ZERO front vector, and this divides zero by zero and returns NaN in
     * every component. FollowCameraController then computes the camera position from it, and
     * the whole camera is NaN from that frame on.
     *
     * Observed exactly that way: GetPlayerPosition reported a NaN saved orientation at startup,
     * and one touch later the camera position was <NaN, NaN, NaN> while the previous frame's
     * was an ordinary <92.03, 115.16, 222.37>.
     *
     * The second degenerate case is a front vector parallel to up - looking straight up or
     * straight down. Cross(front, up) is then zero, and vuFront becomes zero too, which is not
     * NaN but is just as useless to a caller building a coordinate system from it.
     */
    public static void CreateVectorsFromPlaneFront(in Vector3 vFront, out Vector3 vuFront, out Vector3 vuRight)
    {
        float l = vFront.Length();

        /*
         * Negated on purpose: `l < MinFrontLength` is FALSE for NaN, which would let a NaN input
         * straight through the guard meant to stop it.
         */
        Vector3 vuTmpFront = !(l >= MinFrontLength) ? _vuFrontDefault : vFront / l;

        vuRight = Vector3.Cross(vuTmpFront, _vuUp);

        /*
         * Parallel to up: no horizontal component to build a right vector from. Fall back to the
         * default heading rather than handing back a zero basis.
         */
        if (!(vuRight.LengthSquared() >= MinFrontLength * MinFrontLength))
        {
            vuRight = Vector3.Cross(_vuFrontDefault, _vuUp);
        }

        vuFront = -Vector3.Cross(vuRight, _vuUp);
    }
    
    /*
     * Given a front vector, try to figure out a rotation matrix representing a look into that
     * direction, assuming that "up" should be (0,1,0)-ish.
     */
    public static Matrix4x4 CreateMatrixFromPlaneFront(in Vector3 vFront)
    {
        CreateVectorsFromPlaneFront(vFront, out var vuFront, out var _);
        return Matrix4x4.CreateWorld(Vector3.Zero, vuFront, _vuUp);
    }


    public static Quaternion CreateQuaternionFromPlaneFront(in Vector3 vFront)
    {
        return Quaternion.CreateFromRotationMatrix(CreateMatrixFromPlaneFront(vFront));
    }

    public static void VectorsFromMatrix(in Matrix4x4 m, out Vector3 vFront, out Vector3 vUp, out Vector3 vRight)
    {
        vFront = -new Vector3(m.M31, m.M32, m.M33);
        vUp = new(m.M21, m.M22, m.M23);
        vRight = new(m.M21, m.M22, m.M23);
    }
}