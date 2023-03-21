using System;
using System.Numerics;
using System.Collections.Generic;

namespace Splash;

public interface IThreeD
{
    public void UploadMesh(in AMeshEntry aMeshEntry);
    public AMeshEntry CreateMeshEntry(in engine.joyce.Mesh jMesh);
    public void DestroyMeshEntry(in AMeshEntry aMeshEntry);

    public AMaterialEntry GetDefaultMaterial();

    public AMaterialEntry CreateMaterialEntry(in engine.joyce.Material jMaterial);
    public void FillMaterialEntry(in AMaterialEntry aMaterialEntry);
    public void UnloadMaterialEntry(in AMaterialEntry aMaterialEntry);

    public ATextureEntry CreateTextureEntry(in engine.joyce.Texture jTexture);
    public void FillTextureEntry(in ATextureEntry aTextureEntry);
    
    public void ApplyAllLights(in IList<Light> listLights, in AShaderEntry aShaderEntry);
    public void ApplyAmbientLights(in Vector4 colAmbient, in AShaderEntry aShaderEntry);
    public void SetCameraPos(in Vector3 vCamera);
    public void DrawMeshInstanced(
        in AMeshEntry aMeshEntry,
        in AMaterialEntry aMaterialEntry,
        in Span<Matrix4x4> spanMatrices,
        in int nMatrices);
}