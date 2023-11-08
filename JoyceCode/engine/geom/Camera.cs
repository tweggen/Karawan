using System.Numerics;

namespace engine.geom;

public static class Camera
{
    /*
     * Given a front vector, try to figure out a rotation matrix representing a look into that
     * direction, assuming that "up" should be (0,1,0)-ish.
     */
    static Matrix4x4 CreateMatrixFromFront(in Vector3 vFront)
    {
        var vuFront = vFront / vFront.Length();
        Vector3 vuUp = new Vector3(0f, 1f, 0f);
        var vuPerfectRight = Vector3.Cross(vuFront, vuUp);
        var vuPerfectFront = -Vector3.Cross(vuPerfectRight, vuUp);
        return Matrix4x4.CreateWorld(
            Vector3.Zero, vuPerfectFront, vuUp);
    }
}