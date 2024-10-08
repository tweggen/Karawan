using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
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
        return $"5100/PlaceDebrisOperator/{_myKey}";
    }

    public Func<Task> FragmentOperatorApply(Fragment worldFragment, FragmentVisibility visib) => new (async () =>
    {
        if (0 == (visib.How & engine.world.FragmentVisibility.Visible3dAny))
        {
            return;
        }
        
        builtin.tools.RandomSource rnd = new(_myKey+worldFragment.GetId());
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

            var epx = I.Get<engine.world.MetaGen>().Loader.GetElevationPixelAt(
                worldFragment.Position + vCenter);
            /*
             * Only place them outside a cluster, not in trails or other things.
             */
            if (epx.Biome != 0)
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
                vRock.Y = I.Get<engine.world.MetaGen>().Loader.GetHeightAt(
                              worldFragment.Position.X+vRock.X, 
                              worldFragment.Position.Z+vRock.Z)
                          + debrisSize/3f;

                Mesh m = engine.joyce.mesh.Tools.CreateCubeMesh("debris", debrisSize);
                m.Transform(
                    Matrix4x4.CreateRotationX(rnd.GetFloat() * Single.Pi * 2f)
                    * Matrix4x4.CreateRotationY(rnd.GetFloat() * Single.Pi * 2f)
                    * Matrix4x4.CreateTranslation(vRock));
                
                matmesh.Add(I.Get<ObjectRegistry<Material>>().Get("nogame.terrain.debris.materials.debris"), m);
            }

        }
            
        if (matmesh.IsEmpty())
        {
            return;
        }
        matmesh = MatMesh.CreateMerged(matmesh);
        worldFragment.AddStaticInstance("debris", InstanceDesc.CreateFromMatMesh(matmesh, 800f));
    });

    
    public PlaceDebrisOperator(string strKey)
    {
        _myKey = strKey;
        
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.terrain.debris.materials.debris",
            name => new engine.joyce.Material()
            {
                Texture = I.Get<TextureCatalogue>().FindColorTexture(0xffaa6688)
            });
    }
    

    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new PlaceDebrisOperator(
            (string)p["strKey"]);
    }

}