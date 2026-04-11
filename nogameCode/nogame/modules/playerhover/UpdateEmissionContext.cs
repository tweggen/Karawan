using System.Numerics;
using engine;
using engine.joyce.components;

namespace nogame.modules.playerhover;

public class UpdateEmissionContext : AController
{
    protected override void OnLogicalFrame(object? source, float dt)
    {
        var ectx = new engine.news.EmissionContext();
        {
            if (_engine.Player.TryGet(out var ePlayer) && ePlayer.Has<Transform3ToWorld>())
            {
                ectx.PlayerPos = ePlayer.Get<Transform3ToWorld>().Matrix.Translation;
            }
        }
        {
            if (_engine.Camera.TryGet(out var eCamera) && eCamera.Has<Transform3ToWorld>())
            {
                var camMatrix = eCamera.Get<Transform3ToWorld>().Matrix;
                ectx.CameraPos = camMatrix.Translation;
                // Row 3 of the transform matrix is the forward (-Z) direction
                ectx.CameraForward = -new Vector3(camMatrix.M31, camMatrix.M32, camMatrix.M33);
            }
        }
        I.Get<engine.news.EmissionContext>()?.UpdateFrom(ectx);
    }
}