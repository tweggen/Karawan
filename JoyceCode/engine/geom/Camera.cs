using System.Numerics;

namespace engine.geom;

public static class Camera
{
    static private Vector3 _vuUp = new(0f, 1f, 0f);
    public static void CreateVectorsFromPlaneFront(in Vector3 vFront, out Vector3 vuFront, out Vector3 vuRight)
    {
        var vuTmpFront = vFront / vFront.Length();
        vuRight = Vector3.Cross(vuTmpFront, _vuUp);
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