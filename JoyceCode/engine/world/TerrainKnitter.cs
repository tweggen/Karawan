using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using engine.joyce;
using Newtonsoft.Json;

namespace engine.world
{
    public class TerrainKnitter
    {
        public static joyce.Mesh BuildMolecule(
            engine.elevation.ElevationPixel[,] elevations,
            int coarseness,
            Material jMaterial
        )
        {
            Texture jTexture = jMaterial.Texture;
            if (null == jTexture) jTexture = jMaterial.EmissiveTexture;
            Vector2 uvMin, uvMax;

            if (null != jTexture)
            {
                uvMin = jTexture.InvSize2;
                uvMax = Vector2.One - jTexture.InvSize2;
            }
            else
            {
                uvMin = Vector2.Zero;
                uvMax = Vector2.One;
            }
            
            var g = joyce.Mesh.CreateListInstance("terrainknitter");

            var groundResolution = world.MetaGen.GroundResolution; // 20

            uint coarseResolution = (uint)(groundResolution / coarseness);
            
            /*
             * Because we need to build the entire mesh up to the beginning of the next one.
             */
            //uint coarseNElevations = coarseResolution;
            // trace('groundResolution: '+groundResolution+", coarseResolution: "+coarseResolution);

            var getLocal = (int iterX, int iterY) =>
            {
                int x0 = (int) (((iterX+0) * groundResolution) / coarseResolution);
                int x1 = (int) (((iterX+1) * groundResolution) / coarseResolution);

                int y0 = (int)(((iterY+0) * groundResolution) / coarseResolution);
                int y1 = (int) (((iterY+1) * groundResolution) / coarseResolution);

                float localY = 0f;

                if(1==coarseness) {
                    localY = elevations[iterY,iterX].Height;
                } else {
                    int n  = 0;
                    for(int ey=y0; ey<y1; ey++ ) {
                        for(int ex=x0; ex<x1; ex++ ) {
                            // trace("ex: "+ex+", ey="+ey+", localY="+localY);
                            localY += elevations[ey,ex].Height;
                            ++n;
                        }
                    }
                    localY = localY / n;
                }
                if (localY > -0.00001 && localY < 0.00001)
                {
                    // trace( "Warning: elevation ca. 0 at "+iterX+", "+iterY );
                }

                return new Vector3(
                    -world.MetaGen.FragmentSize/2.0f // -100
                    + (world.MetaGen.FragmentSize* iterX/coarseResolution),
                    localY,
                    -world.MetaGen.FragmentSize/2.0f // -100
                    + (world.MetaGen.FragmentSize*(iterY)/coarseResolution)
                    );
            };
            
            /*
             * 20m are the width/height of entire 512 pixel size.
             * However, we are going to tile that.
             */
            float texWidth = 20; // That is 20 times per fragment.
            float texHeight = 20;


            for(int iterY=0; iterY<coarseResolution; ++iterY) {
                for(int iterX=0; iterX<coarseResolution; ++iterX ) {

                    Vector3 v3UL = getLocal(iterX, iterY);
                    Vector3 v3UR = getLocal(iterX+1, iterY);
                    Vector3 v3LL = getLocal(iterX, iterY+1);
                    Vector3 v3LR = getLocal(iterX+1, iterY+1);
                    
                    g.p(v3UL);
                    g.p(v3UR);
                    g.p(v3LL);
                    g.p(v3LR);
            
                    /*
                     * At this point, u/v is integer digits.
                     * We need to offset it a little bit to make the "nearest" sampling work in the tiles.
                     * So that the first pixel is 1/128 / 4, and the last pixel 127/128 + 1/128/4
                     */

                    Vector2 v2UL = uvMin;
                    Vector2 v2UR = new(uvMax.X, uvMin.Y);
                    Vector2 v2LL = new(uvMin.X, uvMax.Y);
                    Vector2 v2LR = uvMax;
                    
                    g.UV(v2UL);
                    g.UV(v2UR);
                    g.UV(v2LL);
                    g.UV(v2LR);
                }
            }


            for(uint iterY=0; iterY<coarseResolution; iterY++ )
            {
                for(uint iterX=0; iterX<coarseResolution; iterX++ )
                {
                    uint idx = (iterY * coarseResolution + iterX) * 4;
                    g.Idx(idx + 2, idx + 1, idx + 0);
                    g.Idx(idx + 2, idx + 3, idx + 1);
                }
            }

            return g;
        }

    }
}
