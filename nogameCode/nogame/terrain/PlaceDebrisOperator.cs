using System;
using System.Numerics;
using System.Threading.Tasks;
using engine.geom;
using engine.joyce;
using engine.world;

namespace nogame.terrain;

public class PlaceDebrisOperator : IFragmentOperator
{
    private string _myKey;
    
    public void FragmentOperatorGetAABB(out AABB aabb)
    {
        /*
         * Unfortunately, we apply everywhere.
         */
        aabb = AABB.All;
    }

    public string FragmentOperatorGetPath()
    {
        return $"5100/PlaceDebrisOperator/{_myKey}/";
    }

    public Task FragmentOperatorApply(Fragment worldFragment) => new Task(() =>
    {
        engine.RandomSource rnd = new(_myKey+worldFragment.GetId());
        AABB aabbFragment = worldFragment.AABB;
        MatMesh matmesh = new();
        
        /*
         * Iterate random spots, placing debris there.
         */
        // TXWTODO: Isn't that more like a fragment biome classification.
        float tmp = (rnd.GetFloat() * 4f);
        tmp *= tmp;
        int nDebris = Int32.Max(0, (int) (tmp)-3);

        for (int i = 0; i < nDebris; ++i)
        {
            int nRocks = 1 + (int)(rnd.GetFloat() * 10f);
            Vector3 vCenter = new(
                rnd.GetFloat() * MetaGen.FragmentSize - MetaGen.FragmentSize / 2f,
                0f,
                rnd.GetFloat() * MetaGen.FragmentSize - MetaGen.FragmentSize / 2f);
            if (ClusterList.Instance().GetClusterAt(vCenter + worldFragment.Position) != null)
            {
                continue;
            }

            for (int j = 0; j < nRocks; ++j)
            {
                float debrisSize = rnd.GetFloat() * 3f;
                debrisSize *= debrisSize;
                Vector3 vRock = vCenter + new Vector3( 
                    rnd.GetFloat() * nRocks*2f - nRocks*4f,
                    0f,
                    rnd.GetFloat() * nRocks*2f - nRocks*4f
                );
                vRock.Y = engine.world.MetaGen.Instance().Loader.GetHeightAt(
                              worldFragment.Position.X+vRock.X, 
                              worldFragment.Position.Z+vRock.Z)
                          + debrisSize/2f;

                Mesh m = engine.joyce.mesh.Tools.CreateCubeMesh("debris", debrisSize);
                m.Transform(
                    Matrix4x4.CreateRotationX(rnd.GetFloat() * Single.Pi * 2f)
                    * Matrix4x4.CreateRotationY(rnd.GetFloat() * Single.Pi * 2f)
                    * Matrix4x4.CreateTranslation(vRock));
                
                matmesh.Add(engine.joyce.MaterialCache.Get("nogame.terrain.debris.materials.debris"), m);
            }

        }
            
        if (matmesh.IsEmpty())
        {
            return;
        }
        matmesh = MatMesh.CreateMerged(matmesh);
        worldFragment.AddStaticInstance("debris", InstanceDesc.CreateFromMatMesh(matmesh));
    });

    public PlaceDebrisOperator(string strKey)
    {
        _myKey = strKey;
        
        engine.joyce.MaterialCache.Register("nogame.terrain.debris.materials.debris",
            name => new engine.joyce.Material()
            {
                AlbedoColor = 0xffaa6688
            });

    }
}