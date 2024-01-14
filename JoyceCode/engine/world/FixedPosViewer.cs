using System.Collections.Generic;
using System.Numerics;

namespace engine.world;

public class FixedPosViewer : IViewer
{
    private Engine _engine;
    
    public required Vector3 Position;
    public byte How = (byte)(FragmentVisibility.Visible2dNow | FragmentVisibility.Visible3dNow);
    public int Range = 2;


    public void GetVisibleFragments(ref IList<FragmentVisibility> lsVisib)
    {
        engine.joyce.Index3 i3Frag = new(Position / MetaGen.FragmentSize); 

        for (int dz = -Range; dz <= Range; dz++)
        {
            for (int dx = -Range; dx <= Range; dx++)
            {
                lsVisib.Add(new ()
                {
                    How = this.How,
                    I = (short) (i3Frag.I + dx),
                    K = (short) (i3Frag.K + dz)
                });        
            }
        }
    }
    
    
    public FixedPosViewer(Engine engine0)
    {
        _engine = engine0;
    }
}