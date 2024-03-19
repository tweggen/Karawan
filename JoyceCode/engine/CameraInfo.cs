using System.Numerics;
using engine.joyce.components;

namespace engine;

public class CameraInfo
{
    public bool IsValid;
    public DefaultEcs.Entity Entity;
    public Camera3 CCamera3;
    public Transform3ToWorld CTransform3ToWorld;
    public Vector3 Position;
    public Vector3 Front;
    public Vector3 Up;
    public Vector3 Right;


    public CameraInfo(DefaultEcs.Entity e)
    {
        Entity = e;
        if (Entity.IsAlive && Entity.Has<Camera3>() && Entity.Has<Transform3ToWorld>())
        {
            IsValid = true;
            CCamera3 = Entity.Get<Camera3>();
            CTransform3ToWorld = Entity.Get<Transform3ToWorld>();
            Position = CTransform3ToWorld.Matrix.Translation;
            Front = new Vector3(CTransform3ToWorld.Matrix.M31, CTransform3ToWorld.Matrix.M32, CTransform3ToWorld.Matrix.M33);
            Up = new Vector3(CTransform3ToWorld.Matrix.M21, CTransform3ToWorld.Matrix.M22, CTransform3ToWorld.Matrix.M23);
            Right = new Vector3(CTransform3ToWorld.Matrix.M11, CTransform3ToWorld.Matrix.M12, CTransform3ToWorld.Matrix.M13);
        }
        else
        {
            IsValid = false;
        }
    }
}