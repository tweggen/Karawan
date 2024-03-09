using System;
using engine;
using engine.draw;
using engine.joyce.components;
using System.Numerics;

namespace nogame.modules.osd;

public class Compass : engine.AModule
{
    private DefaultEcs.Entity _eCompassDisplay;


    private readonly float _compassCircleLength = 12 * 4;
    private string _strCompass = "N  .  .  .  E  .  .  .  S  .  .  .  W  .  .  .  N  .  .  .  E .  .  .  S  .  .  .  W  .  .  .  ";
    private string _lastCompass = "";
    
    private void _onLogical(object? sender, float dt)
    {
        var ePlayer = _engine.GetPlayerEntity();
        if (!ePlayer.IsAlive || !ePlayer.Has<Transform3ToWorld>())
        {
            return;
        }

        var cTransform3ToWorld = ePlayer.Get<Transform3ToWorld>();
        engine.geom.Camera.VectorsFromMatrix(cTransform3ToWorld.Matrix, out var vFront, out var _, out var _);
        float degree = Single.Atan2Pi(-vFront.X, vFront.Z) / 2.0f * _compassCircleLength;
        if (degree < 0f) degree += _compassCircleLength;
        string compass = _strCompass.Substring(((int)(degree + 0.5f))%(int)_compassCircleLength, (int)(_compassCircleLength-1));

        if (_lastCompass != compass)
        {     
            _eCompassDisplay.Set(new engine.draw.components.OSDText(
                new Vector2(768/2f-64f, 48f),
                new Vector2(128f, 16f),
                compass,
                10,
                0xff448822,
                0x00000000,
                HAlign.Center
            ));
            _lastCompass = compass;
        }
    }


    private void _disposeEntites()
    {
        _eCompassDisplay.Dispose();
    }
    
    
    public override void Dispose()
    {
        _disposeEntites();        
    }
    
    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        _engine.OnLogicalFrame -= _onLogical;

        _disposeEntites();
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);

        _eCompassDisplay = _engine.CreateEntity("OsdCompassDisplay");
        _engine.OnLogicalFrame += _onLogical;
    }
}