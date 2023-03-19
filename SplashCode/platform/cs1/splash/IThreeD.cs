using System.Numerics;

namespace Karawan.platform.cs1.splash;

public interface IThreeD
{
    public void UploadMesh(in AMeshEntry aMeshEntry);
    public AMeshEntry CreateMeshEntry(in engine.joyce.Mesh jMesh);
    public void DestroyMeshEntry(in AMeshEntry aMeshEntry);

    public AMaterialEntry GetDefaultMaterial();

    public AMaterialEntry CreateMaterialEntry(in engine.joyce.Material jMaterial);
    public void FillMaterialEntry(in AMaterialEntry aMaterialEntry);
    public void UnloadMaterialEntry(in AMaterialEntry aMaterialEntry);

    public void SetCameraPos(in Vector3 vCamera);
}