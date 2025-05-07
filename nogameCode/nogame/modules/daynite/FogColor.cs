using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.joyce.components;
using engine.news;

namespace nogame.modules.daynite;

public class FogColor : AController
{
    public DefaultEcs.Entity EFogCamera = default;

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<nogame.modules.daynite.Controller>(),
        new SharedModule<nogame.modules.World>()
    };

        
    private Vector4 _colorBlend(float a, float b, TimeSpan now, Vector4 va, Vector4 vb)
    {
        float x = (float)now.TotalMilliseconds;
        x -= a*3600000;
        x /= b*3600000 - a*3600000;
        return vb * x + va * (1f - x);
    }


    private Vector4 v4FogNight = new(0.00f, 0.00f, 0.02f, 0f);
    private Vector4 v4FogDawn = new(0.2f, 0.11f, 0.2f, 0f); 
    private Vector4 v4FogDay = new(0.4f, 0.3f, 0.3f, 0f);
    private Vector4 v4FogDusk = new(0.3f, 0.25f, 0.2f, 0f);
    private Vector4 v4AmbientNight = new(0f, 0f, 0f, 0.008f);
    private Vector4 v4AmbientDawn = new(0f, 0.003f, 0.0015f, 0f); 
    private Vector4 v4AmbientDay = new(0.015f, 0.015f, 0.015f, 0f);
    private Vector4 v4AmbientDusk = new(0.01f, 0.0f, 0.01f, 0f);


    private void _updateFog(TimeSpan now)
    {
        if (!_engine.Camera.TryGet(out EFogCamera))
        {
            return;
        }

        Vector4 v4FogColor;
        if (now.Hours < 6f || now.Hours > 19f)
        {
            v4FogColor = v4FogNight;
        }
        else if (now.Hours < 8f)
        {
            v4FogColor = _colorBlend(6f, 8f, now, v4FogNight, v4FogDawn);
        }
        else if (now.Hours < 11f)
        {
            v4FogColor = _colorBlend(8f, 11f, now, v4FogDawn, v4FogDay);
        }
        else if (now.Hours < 15f)
        {
            v4FogColor = _colorBlend(11f, 15f, now, v4FogDay, v4FogDusk);
        }
        else if (now.Hours < 19f)
        {
            v4FogColor = _colorBlend(15f, 19f, now, v4FogDusk, v4FogNight);
        }
        else
        {
            v4FogColor = v4FogNight;
        }

        ref var cCamera3 = ref EFogCamera.Get<Camera3>();
        cCamera3.Fog = new Vector4(v4FogColor.X, v4FogColor.Y, v4FogColor.Z, cCamera3.Fog.W);
    }


    private void _updateLights(TimeSpan now)
    {
        Vector4 col4Ambient;
        
        if (now.Hours < 6f || now.Hours > 19f)
        {
            col4Ambient = v4AmbientNight;
        }
        else if (now.Hours < 8f)
        {
            col4Ambient = _colorBlend(6f, 8f, now, v4AmbientNight, v4AmbientDawn);
        }
        else if (now.Hours < 11f)
        {
            col4Ambient = _colorBlend(8f, 11f, now, v4AmbientDawn, v4AmbientDay);
        }
        else if (now.Hours < 15f)
        {
            col4Ambient = _colorBlend(11f, 15f, now, v4AmbientDay, v4AmbientDusk);
        }
        else if (now.Hours < 19f)
        {
            col4Ambient = _colorBlend(15f, 19f, now, v4AmbientDusk, v4AmbientNight);
        }
        else
        {
            col4Ambient = v4AmbientNight;
        }

        I.Get<EventQueue>().Push(new Event("nogame.scenes.root.setAmbientLight", Color.Vector4ToString(col4Ambient)));
    }
    

    protected override void OnLogicalFrame(object? sender, float dt)
    {
        /*
         * Read the current in-game time to set the day-night cycle color. 
         */
        var gameNow = M<nogame.modules.daynite.Controller>().GameNow;
        var now = gameNow.TimeOfDay;

        _updateFog(now);
        _updateLights(now);
    }
}