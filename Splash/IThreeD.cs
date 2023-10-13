using System;
using System.Numerics;
using System.Collections.Generic;

namespace Splash;

public interface IThreeD
{
    public engine.Engine Engine { get; }
    
    public AMeshEntry CreateMeshEntry(in engine.joyce.Mesh jMesh);
    public void UploadMesh(in AMeshEntry aMeshEntry);
    public void UnloadMeshEntry(in AMeshEntry aMeshEntry);

    public AMaterialEntry GetDefaultMaterial();

    public AMaterialEntry CreateMaterialEntry(in engine.joyce.Material jMaterial);
    public void FillMaterialEntry(in AMaterialEntry aMaterialEntry);
    public void UnloadMaterialEntry(in AMaterialEntry aMaterialEntry);

    public ATextureEntry CreateTextureEntry(in engine.joyce.Texture jTexture);
    public void FillTextureEntry(in ATextureEntry aTextureEntry);

    public ARenderbufferEntry CreateRenderbuffer(in engine.joyce.Renderbuffer jRenderbuffer);
    public void UploadRenderbuffer(in ARenderbufferEntry aRenderbufferEntry);
    public void UnloadRenderbuffer(in ARenderbufferEntry aRenderbufferEntry);
    
    public void ApplyAllLights(in IList<Light> listLights, in AShaderEntry aShaderEntry);
    public void ApplyAmbientLights(in Vector4 colAmbient, in AShaderEntry aShaderEntry);
    public void SetCameraPos(in Vector3 vCamera);
    
    public void DrawMeshInstanced(
        in AMeshEntry aMeshEntry,
        in AMaterialEntry aMaterialEntry,
        in Span<Matrix4x4> spanMatrices,
        in int nMatrices);

    public void FinishUploadOnly(in AMeshEntry aMeshEntry);
}