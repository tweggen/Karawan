using System;
using System.Numerics;
using System.Collections.Generic;

namespace Splash;


public interface IThreeD
{
    public engine.Engine Engine { get; }
    
    /**
     * Create a platform data structure for this object. It does not need to filled with anything.
     * It just is a representation of data required by Splash implementation that is specific and
     * unique for this set of parameters.
     */
    public AMeshEntry CreateMeshEntry(in AMeshParams aMeshParams);
    
    /**
     * Prepare a platform data structure for upload. This triggers the computation of any platform
     * specific data that later might be uploaded. It is not required to complete synchronously.
     */
    public void FillMeshEntry(in AMeshEntry aMeshEntry);
    
    /**
     * Upload the platform specific data for use on the GPU.
     */
    public void UploadMeshEntry(in AMeshEntry aMeshEntry);
    
    /**
     * Unload any GPU data from the GPU.
     */
    public void UnloadMeshEntry(in AMeshEntry aMeshEntry);

    public AMaterialEntry GetDefaultMaterial();

    public AMaterialEntry CreateMaterialEntry(in engine.joyce.Material jMaterial);
    public void FillMaterialEntry(in AMaterialEntry aMaterialEntry);
    public void UnloadMaterialEntry(in AMaterialEntry aMaterialEntry);

    public ATextureEntry CreateTextureEntry(in engine.joyce.Texture jTexture);
    public void FillTextureEntry(in ATextureEntry aTextureEntry);
    public void UploadTextureEntry(in ATextureEntry aTextureEntry);

    public ARenderbufferEntry CreateRenderbuffer(in engine.joyce.Renderbuffer jRenderbuffer);
    public void UploadRenderbuffer(in ARenderbufferEntry aRenderbufferEntry);
    public void UnloadRenderbuffer(in ARenderbufferEntry aRenderbufferEntry);
    
    public void SetCameraPos(in Vector3 vCamera);
    
    public void DrawMeshInstanced(
        in AMeshEntry aMeshEntry,
        in AMaterialEntry aMaterialEntry,
        in Span<Matrix4x4> spanMatrices,
        in int nMatrices);

    public void FinishUploadOnly(in AMeshEntry aMeshEntry);

    public void Execute(Action action);
}