using engine;
using engine.joyce.components;

namespace nogame.modules.playerhover;

public class UpdateEmissionContext : AModule
{
    void _onLogicalFrame(object? source, float dt)
    {
        var ectx = new engine.news.EmissionContext();
        {
            if (_engine.Player.TryGet(out var ePlayer) && ePlayer.Has<Transform3ToWorld>())
            {
                ectx.PlayerPos = ePlayer.Get<Transform3ToWorld>().Matrix.Translation;
            }
        }
        {
            if (_engine.Camera.TryGet(out var eCamera))
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