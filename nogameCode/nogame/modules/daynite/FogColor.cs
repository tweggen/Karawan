using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.joyce.components;

namespace nogame.modules.daynite;

public class FogColor : AModule
{
    public DefaultEcs.Entity EFogCamera = default;

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.daynite.Module>()
    };

        
    private Vector3 _colorBlend(float a, float b, TimeSpan now, Vector3 va, Vector3 vb)
    {
        float x = (float)now.TotalMilliseconds;
        x -= a*3600000;
        x /= b*3600000 - a*3600000;
        return vb * x + va * (1f - x);
    }


    private Vector3 v3FogNight = new(0.00f, 0.00f, 0.02f);
    private Vector3 v3FogDawn = new(0.2f, 0.11f, 0.2f); 
    private Vector3 v3FogDay = new(0.4f, 0.3f, 0.3f);
    private Vector3 v3FogDusk = new(0.3f, 0.25f, 0.2f);


    private void _onLogicalFrame(object? sender, float dt)
    {
        EFogCamera = _engine.GetCameraEntity();
        if (!EFogCamera.IsAlive && EFogCamera.Has<Camera3>())
        {
            return;
        }

        var gameNow = M<nogame.modules.daynite.Module>().GameNow;
        var now = gameNow.TimeOfDay;

        {
            Vector3 fogColor;
            if (now.Hours < 6f || now.Hours > 19f)
            {
                fogColor = v3FogNight;
            }
            else if (now.Hours < 8f)
            {
                fogColor = _colorBlend(6f, 8f, now, v3FogNight, v3FogDawn);
            }
            else if (now.Hours < 11f)
            {
                fogColor = _colorBlend(8f, 11f, now, v3FogDawn, v3FogDay);
            }
            else if (now.Hours < 15f)
            {
                fogColor = _colorBlend(11f, 15f, now, v3FogDay, v3FogDusk);
            }
            else if (now.Hours < 19f)
            {
                fogColor = _colorBlend(15f, 19f, now, v3FogDusk, v3FogNight);
            }
            else
            {
                fogColor = v3FogNight;
            }

            ref var cCamera3 = ref EFogCamera.Get<Camera3>();
            cCamera3.Fog = new Vector4(fogColor.X, fogColor.Y, fogColor.Z, cCamera3.Fog.W);
        }
    }

    
    public override void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);
        _engine.OnLogicalFrame += _onLogicalFrame;
    }
}