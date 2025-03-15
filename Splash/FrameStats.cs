namespace Splash;

public class FrameStats
{
    public int NEntities = 0;
    public int NMeshes = 0;
    public int NMaterials = 0;
    public int NInstances = 0;
    public int NAnimations = 0;
    public int NSkippedMeshes = 0;
    public int NSkippedMaterials = 0;
    public int NTriangles = 0;

    public static FrameStats operator +(FrameStats a, FrameStats b)
    {
        return new FrameStats()
        {
            NEntities = a.NEntities + b.NEntities,
            NMeshes = a.NMeshes + b.NMeshes,
            NMaterials = a.NMaterials + b.NMaterials,
            NInstances = a.NInstances + b.NInstances,
            NSkippedMeshes = a.NSkippedMeshes + b.NSkippedMeshes,
            NSkippedMaterials = a.NSkippedMaterials + b.NSkippedMaterials,
            NTriangles = a.NTriangles + b.NTriangles
        };
    }

    
    public static FrameStats operator -(FrameStats a, FrameStats b)
    {
        return new FrameStats()
        {
            NEntities = a.NEntities - b.NEntities,
            NMeshes = a.NMeshes - b.NMeshes,
            NMaterials = a.NMaterials - b.NMaterials,
            NInstances = a.NInstances - b.NInstances,
            NSkippedMeshes = a.NSkippedMeshes - b.NSkippedMeshes,
            NSkippedMaterials = a.NSkippedMaterials - b.NSkippedMaterials,
            NTriangles = a.NTriangles - b.NTriangles
        };
    }

    
    public static FrameStats operator /(FrameStats a, int n)
    {
        return new FrameStats()
        {
            NEntities = a.NEntities / n,
            NMeshes = a.NMeshes / n,
            NMaterials = a.NMaterials / n,
            NInstances = a.NInstances / n,
            NSkippedMeshes = a.NSkippedMeshes / n,
            NSkippedMaterials = a.NSkippedMaterials / n,
            NTriangles = a.NTriangles / n
        };
    }


    public override string ToString()
    {
        return $"{NEntities} en, {NMaterials} mat, {NMeshes} mesh, {NInstances} inst, {NTriangles} tri, 1 sha.";
    }


    public FrameStats()
    {
    }
    

    public FrameStats(FrameStats a)
    {
        NEntities = a.NEntities;
        NMeshes = a.NMeshes;
        NMaterials = a.NMaterials;
        NInstances = a.NInstances;
        NSkippedMeshes = a.NSkippedMeshes;
        NSkippedMaterials = a.NSkippedMaterials;
        NTriangles = a.NTriangles;
    }
}