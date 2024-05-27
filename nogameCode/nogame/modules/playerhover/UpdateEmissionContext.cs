using engine;
using engine.joyce.components;

namespace nogame.modules.playerhover;

public class UpdateEmissionContext : AModule
{
    void _onLogicalFrame(object? source, float dt)
    {
        var ectx = new engine.news.EmissionContext();
        {
            var ePlayer = _engine.GetPlayerEntity();
            if (ePlayer.IsAlive && ePlayer.Has<Transform3ToWorld>())
            {
                ectx.PlayerPos = ePlayer.Get<Transform3ToWorld>().Matrix.Translation;
            }
        }
        {
            var eCamera = _engine.GetCameraEntity();
            if (eCamera.IsAlive && eCamera.Has<Transform3ToWorld>())
            {
                ectx.CameraPos = eCamera.Get<Transform3ToWorld>().Matrix.Translation;
            }
        }
        I.Get<engine.news.EmissionContext>()?.UpdateFrom(ectx);
    }

    public override void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}