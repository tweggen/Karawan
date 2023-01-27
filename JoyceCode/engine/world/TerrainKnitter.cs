using Java.Lang;
using Java.Util.Functions;
using System;
using System.Collections.Generic;
using System.Text;

namespace engine.world
{
    public class TerrainKnitter
    {
        public static joyce.Mesh BuildMolecule(
            float[,] elevations,
            int coarseness //,
            //string materialId
        )
        {
            var g = joyce.Mesh.CreateArrayListInstance();

            var groundResolution = world.MetaGen.GroundResolution;

            int coarseResolution = (int)(groundResolution / coarseness);
            int coarseNElevations = coarseResolution + 1;
            // trace('groundResolution: '+groundResolution+", coarseResolution: "+coarseResolution);

            // var verts:Array<Float> = new Array<Float>();
            for(int iterY=0; iterY<coarseNElevations; ++iterY) {
                int y0 = (int)(((iterY+0) * groundResolution) / coarseResolution);
                int y1 = (int) (((iterY+1) * groundResolution) / coarseResolution);
                for(int iterX=0; iterX<coarseNElevations; ++iterX ) {
                    float localX = -world.MetaGen.FragmentSize/2.0f
                        + (world.MetaGen.FragmentSize* iterX/coarseResolution);
                    float localZ = -world.MetaGen.FragmentSize/2.0f
                        + (world.MetaGen.FragmentSize*(iterY)/coarseResolution);
                    int x0 = (int) (((iterX+0) * groundResolution) / coarseResolution);
                    int x1 = (int) (((iterX+1) * groundResolution) / coarseResolution);
                    // trace("iterX: "+iterX+", x0: "+x0+", x1: "+x1);

                    float localY = 0f;

                    if(1==coarseness) {
                        localY = elevations[iterY,iterX];
                    } else {
                        int n  = 0;
                        for(int ey=y0; ey<y1; ey++ ) {
                            for(int ex=x0; ex<x1; ex++ ) {
                                // trace("ex: "+ex+", ey="+ey+", localY="+localY);
                                localY += elevations[ey,ex];
                                ++n;
                            }
                        }
                        localY = localY / n;
                    }
                    if (localY > -0.00001 && localY < 0.00001)
                    {
                    // trace( "Warning: elevation ca. 0 at "+iterX+", "+iterY );
                    }
                    g.p(localX, localY, localZ);
                    //trace( "x: "+localX+", y: "+localY+", z: "+localZ );
                }
            }

            /*
            * 25m are the width/height of entire 512 pixel size.
            * However, we are going to tile that.
            */
            float texWidth = 20;
            float texHeight = 20;

            // var uvs:Array<Float> = new Array<Float>();
            for (int iterY=0; iterY<coarseNElevations; ++iterY )
            {
                for (int iterX=0; iterX<coarseNElevations; ++iterX )
                {
                    float localU =
                        (iterX * world.MetaGen.FragmentSize)
                            / coarseResolution / texWidth;
                    float localV =
                        (iterY * world.MetaGen.FragmentSize)
                            / coarseResolution / texHeight;

                    /*
                     * At this point, u/v is integer digits.
                     * We need to offset it a little bit to make the "nearest" sampling work in the tiles.
                     * So that the first pixel is 1/128 / 4, and the last pixel 127/128 + 1/128/4
                     */
                    //localU += (1./128.)*0.25;
                    g.UV(localU, localV);
                }
            }


            // var indices:Array<UInt> = new Array<UInt>();
            int w = coarseNElevations;
            for(int iterY=0; iterY<coarseResolution; iterY++ )
            {
                for(int iterX=0; iterX<coarseResolution; iterX++ )
                {
                    g.Idx(
                        (iterY + 1) * w + iterX,
                        iterY * w + iterX + 1,
                        iterY * w + iterX
                    );
                    g.Idx(
                        (iterY + 1) * w + iterX,
                        (iterY + 1) * w + iterX + 1,
                        iterY * w + iterX + 1
                    );
                }
            }

            return g;
        }

    }
}
