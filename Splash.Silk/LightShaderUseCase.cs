using System.Numerics;
using Silk.NET.OpenGL;
using static Splash.Silk.GLCheck;
using static engine.Logger;

namespace Splash.Silk;

public class LightShaderPos
{
    // Shader locations
    public int EnabledLoc;
    public int TypeLoc;
    public int PosLoc;
    public int TargetLoc;
    public int ColorLoc;
    public int Param1Loc;
}


public class LightShaderUseCaseLocs : ShaderLocs
{
    public LightShaderPos[] Pos;
    public int AmbientLoc;

    private unsafe void _applyLightValues(GL gl, in SkProgramEntry sh, int index, in Light light)
    {
        try
        {
            var lightShaderPos = Pos[index];
            bool checkLights = false;

            // Send to shader light enabled state and type
            sh.SetUniform(lightShaderPos.EnabledLoc, (light.enabled ? 1 : 0));
            if (checkLights) CheckError(gl,$"Set Uniform light enabled {index}");
            sh.SetUniform(lightShaderPos.TypeLoc, (int)light.type);
            if (checkLights) CheckError(gl,$"Set Uniform light type {index}");

            // Send to shader light position values
            Vector3 position = new(light.position.X, light.position.Y, light.position.Z);
            sh.SetUniform(lightShaderPos.PosLoc, position);
            if (checkLights) CheckError(gl,$"Set Uniform light position {index}");

            // Send to shader light target position values
            Vector3 target = new(light.target.X, light.target.Y, light.target.Z);
            sh.SetUniform(lightShaderPos.TargetLoc, target);
            if (checkLights) CheckError(gl,$"Set Uniform light target {index}");

            // Send to shader light color values
            Vector4 color = light.color;
            sh.SetUniform(lightShaderPos.ColorLoc, color);
            if (checkLights) CheckError(gl,$"Set Uniform light color {index}");

            float param1 = light.param1;
            sh.SetUniform(lightShaderPos.Param1Loc, param1);
        }
        catch (Exception e)
        {
            Error("Problem applying light values");
            // throw e;
        }
    }

    public void Apply(GL gl, SkProgramEntry sh, LightCollector lightCollector)
    {
        if (null == sh)
        {
            return;
        }

        sh.SetUniform(AmbientLoc, lightCollector.ColAmbient);
        // CheckError(gl, $"Set Uniform ambient light");
        for (int i = 0; i < lightCollector.LightsCount; i++)
        {
            _applyLightValues(gl, sh, i, lightCollector.Lights[i]);
        }
    }
}


public class LightShaderUseCase : IShaderUseCase
{
    public static string StaticName = "LightShader";
    public string Name { get; } = StaticName;

    public override string ToString()
    {
        return Name;
    }
    
    // Create a light and get shader locations
    private void _compileLight(
        in LightShaderPos lightShaderPos, int lightIndex, SkProgramEntry sh)
    {
        string enabledName = $"lights[{lightIndex}].enabled";
        string typeName = $"lights[{lightIndex}].type";
        string posName = $"lights[{lightIndex}].position";
        string targetName = $"lights[{lightIndex}].target";
        string colorName = $"lights[{lightIndex}].color";
        string param1Name = $"lights[{lightIndex}].param1";

        lightShaderPos.EnabledLoc = (int)sh.GetUniform(enabledName);
        lightShaderPos.TypeLoc = (int)sh.GetUniform(typeName);
        lightShaderPos.PosLoc = (int)sh.GetUniform(posName);
        lightShaderPos.TargetLoc = (int)sh.GetUniform(targetName);
        lightShaderPos.ColorLoc = (int)sh.GetUniform(colorName);
        lightShaderPos.Param1Loc = (int)sh.GetUniform(param1Name);
    }

    
    public ShaderLocs Compile(SkProgramEntry sh)
    {
        LightShaderUseCaseLocs l = new();
        l.Pos = new LightShaderPos[LightCollector.MAX_LIGHTS];
        
        /*
         * Don't force setting the lights, if the shader doies not use light, dont set it.
         */
        try
        {
            for (int i = 0; i < LightCollector.MAX_LIGHTS; ++i)
            {
                l.Pos[i] = new();
                _compileLight(l.Pos[i], i, sh);
            }

        }
        catch (Exception e)
        {
            
        }
        try
        {
            l.AmbientLoc = (int)sh.GetUniform("col4Ambient");
        }
        catch (Exception e)
        {
            
        }

        return l;
    }
}