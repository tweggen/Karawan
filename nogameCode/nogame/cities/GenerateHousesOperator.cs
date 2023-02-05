using System;
using System.Collections.Generic;
using System.Numerics;


namespace nogame.cities
{
    public class GenerateHousesOperator
    {
        private void trace(string message)
        {
            Console.WriteLine(message);
        }

        static private object _lock = new();
        static private engine.joyce.Material _jMaterialHouse = null;

        static private engine.joyce.Material _getHouseMaterial()
        {
            lock (_lock)
            {
                if (_jMaterialHouse == null)
                {
                    _jMaterialHouse = new engine.joyce.Material();
                    _jMaterialHouse.AlbedoColor = 0xff444444;
                }
                return _jMaterialHouse;
            }
        }

        private engine.world.ClusterDesc _clusterDesc;
        private engine.RandomSource _rnd;
        private string _myKey;

        /*
         * Relation between basic texture and stories.
         * We do assume that a texture contains a integer number of stories.
         */
        private float _storiesPerTexture = 32f;
        private float _metersPerTexture = 3.0f * 32f;

        public string FragmentOperatorGetPath()
        {
            return $"8001/GenerateHousesOperator/{_myKey}/";
        }


        /**
         * The trivial building texture covers 32 stories by definition.
         * We set a story to 3m. 
         * 
         * @param h0
         *     The height of the building, in meters. This now always is a multiple
         *     of the number of stories in the building.
         * @param mpt
         *     The number of meters per texture.
         */
        private void _createHouseSubGeo(
            in engine.world.Fragment worldFragment,
            in IList<Vector3> p,
            float h0, float mpt,
            in engine.joyce.Mesh g
        )
        {

            /*
             * Construct a path vector for what was originally the height.
             * This shall be the input of the new extrude function.
             */
            var vh = new Vector3(0f, h0, 0f);

            /*
             * Extrude the given polygon along path vector.
             * The polygon is assumed to be coplanar.
             */
            var path = new List<Vector3>();
            path.Add(vh);
        
            /*
             * 27 is the magical number we currently use to identify buildings in collisions.
             */
            var opExtrudePoly = new builtin.tools.ExtrudePoly(p, path, 27, _metersPerTexture, false, false, true);
            try {
                opExtrudePoly.BuildGeom(worldFragment, g);
            } catch (Exception e) {
                trace( $"GenerateHousesOperator.createHouseSubGeo(): buildGeom(): Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
            }
#if false
            try
            {
                opExtrudePoly.buildPhys(worldFragment, mol);
            }
            catch (unknown: Dynamic ) {
                trace('GenerateHousesOperator.createHouseSubGeo(): buildPhys(): Unknown exception applying fragment operator "${fragmentOperatorGetPath()}": '
                    + Std.string(unknown) + "\n"
                    + haxe.CallStack.toString(haxe.CallStack.callStack()));
            }
#endif

        }


        private float GetBaseHeight(
            in engine.world.Fragment worldFragment,
            IList<Vector3> p)
        {
            float xo = worldFragment.Position.X;
            float zo = worldFragment.Position.Z;
            var inFragmentY = 10000f;
            foreach(var p0 in p)
            {
                try
                {
                    var f = worldFragment.Loader.GetHeightAt(xo + p0.X, zo + p0.Z);
                    if (f < inFragmentY) inFragmentY = f;
                }
                catch (Exception e) { }
            }
            return inFragmentY;
        }

#if false
        private function createNeonSignSubGeo(
    allEnv: AllEnv,
        worldFragment: WorldFragment,
        p0: geom.Vector3D, pe: geom.Vector3D,
        h: Float,
        neonMol: engine.SimpleMolecule,
        neonG: engine.PlainGeomAtom
    ) : Void
{
    /*
     * Number of letters.
     */
    var nLetters = 2 + Std.int(_rnd.getFloat() * 8.0);

    var letterHeight = 1.5;

    /*
     * height of first letter.
     */
    var h0 = _rnd.getFloat() * (h - nLetters * letterHeight - 3.0);
    var h1 = h0 + nLetters * letterHeight;

    /*
     * Trivial implementation: Add a part of the texture, which is 8x8
     */
    var i0: Int = neonG.getNextVertexIndex();

    neonG.p(p0.x, p0.y + h0, p0.z);
    neonG.uv(0.0, 1.0 - 0.0);
    neonG.p(p0.x, p0.y + h1, p0.z);
    neonG.uv(0.0, 1.0 - 0.125 * nLetters);
    neonG.p(p0.x + pe.x, p0.y + h1 + pe.y, p0.z + pe.z);
    neonG.uv(0.125, 1.0 - 0.125 * nLetters);
    neonG.p(p0.x + pe.x, p0.y + h0 + pe.y, p0.z + pe.z);
    neonG.uv(0.125, 1.0 - 0.0);

    neonG.idx(i0 + 0, i0 + 1, i0 + 3);
    neonG.idx(i0 + 1, i0 + 2, i0 + 3);
}


/**
 * Create large-scale neon-lights for the given house geometry.
 */
private function createNeonSignsSubGeo(
    allEnv: AllEnv,
        worldFragment: WorldFragment,
        p: Array<geom.Vector3D>,
        h: Float,
        neonMol: engine.SimpleMolecule,
        neonG: engine.PlainGeomAtom
    ) : Void
{
    /*
     * For the neon sign, we each of the corner points, using 1 meter in wall direction to
     * outside to place the rectangle.
     */

    var letterWidth = 1.5;

    var l = p.length;
    for (i in 0...l )
    {
        /*
         * Start point of sign
         */
        var p0 = p[i].clone();
        /*
         * Extent of sign.
         */
        var pe = p[(i + 1) % l].clone();

        pe.subtract(p0);
        pe.normalize();
        pe.scale(-letterWidth);

        createNeonSignSubGeo(
            allEnv,
            worldFragment,
            p0, pe,
            h,
            neonMol,
            neonG);
    }
}
#endif


        public void FragmentOperatorApply(
            engine.world.Fragment worldFragment)
        {
            float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
            float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

            float fsh = engine.world.MetaGen.FragmentSize / 2.0f;

            /*
             * We don't apply the operator if the fragment completely is
             * outside our boundary box (the cluster)
             */
            {
                float csh = _clusterDesc.Size / 2.0f;
                if (
                    (cx - csh) > (fsh)
                    || (cx + csh) < (-fsh)
                    || (cz - csh) > (fsh)
                    || (cz + csh) < (-fsh)
                )
                {
                    return;
                }
            }

            // trace( 'GenerateHousesOperator(): cluster "${_clusterDesc.name}" (${_clusterDesc.id}) in range');
            _rnd.clear();

#if false
            worldFragment.addMaterialFactory(
                "GenerateHousesOperator._matHouse", function() {
                var mat = new engine.Material("");
                // mat.diffuseTexturePath = "building/stealtiles1.jpg";
                // mat.ambientTexturePath = "building/cyanwindow.jpg";
                // mat.diffuseTexturePath = "building/yellowwindows.png";
                mat.diffuseTexturePath = "building/buildingdiffuse.png";
                mat.ambientTexturePath = "building/buildingambient.png";
                mat.textureRepeat = true;
                mat.textureSmooth = false;
                // mat.ambientColor = 0x00ff00;
                mat.ambient = 1.0;
                mat.specular = 0.0;
                return mat;
            }
                );

            var g = new engine.PlainGeomAtom(null, null, null,
                "GenerateHousesOperator._matHouse");
            var mol = new engine.SimpleMolecule( [g] );

            worldFragment.addMaterialFactory(
                "GenerateHousesOperator._matHanyuLorem", function() {
                var mat = new engine.Material("");
                mat.ambientTexturePath = "building/lorem.png";
                mat.diffuseTexturePath = "building/lorem.png";
                mat.textureRepeat = true;
                mat.textureSmooth = false;
                mat.ambient = 10.0;
                mat.specular = 0.0;
                mat.isLight = true;
                mat.isBothSides = true;
                return mat;
            }
                );

            var neonG = new engine.PlainGeomAtom(null, null, null,
                "GenerateHousesOperator._matHanyuLorem");
            var neonMol = new engine.SimpleMolecule( [neonG]);
#endif

            engine.joyce.Mesh g = engine.joyce.Mesh.CreateListInstance();

            /*
             * Iterate through all quarters in the clusters and generate lots and houses.
             */
            var quarterStore = _clusterDesc.quarterStore();

            foreach (var quarter in quarterStore.GetQuarters() )
            {
                if (quarter.IsInvalid())
                {
                    trace( $"GenerateHousesOperator.fragmentOperatorApply(): Skipping invalid quarter.");
                    continue;
                }
                /*
                 * Place on house in each quarter in the middle.    
                 */
                float xmiddle = 0.0f;
                float ymiddle = 0.0f;
                int n = 0;
                var delims = quarter.GetDelims();
                foreach (var delim in delims )
                {
                    xmiddle += delim.StreetPoint.Pos.X;
                    ymiddle += delim.StreetPoint.Pos.Y;
                    ++n;
                }
                // trace( 'middle: $xmiddle, $ymiddle');
                if (3 > n)
                {
                    continue;
                }
                xmiddle /= n;
                ymiddle /= n;

                bool haveHouse = false;


                /*
                 * Compute some properties of this quarter.
                 * - is it convex?
                 * - what is it extend?
                 * - what is the largest side?
                 */
                foreach (var estate in quarter.GetEstates() )
                {

                    /*
                     * Now create a house subgeometry for each of the buildings on the
                     * estate.
                     */
                    foreach (var building in estate.GetBuildings() )
                    {
                        var orgCenter = building.getCenter();
                        var center = orgCenter;
                        center.X += cx;
                        center.Z += cz;
                        if (!worldFragment.IsInsideLocal(center.X, center.Z))
                        {
                            // trace( 'No building ${orgCenter.x}, ${orgCenter.z} (abs ${center.x}, ${center.z})' );
                            continue;
                        }
                        else
                        {
                            // trace( 'Building at ${orgCenter.x}, ${orgCenter.z} (abs ${center.x}, ${center.z})' );
                        }

                        var orgPoints = building.GetPoints();
                        var fragPoints = new List<Vector3>();
                        foreach (var p in orgPoints )
                        {
                            fragPoints.Add(
                                new Vector3(
                                    p.X + cx,
                                    _clusterDesc.AverageHeight + 2.15f,
                                    p.Z + cz
                                )
                            );
                        }
                        var height = building.GetHeight();
                        try
                        {
                            _createHouseSubGeo(
                                worldFragment, fragPoints, height, _metersPerTexture, g);
                        }
                        catch (Exception e) {
                            trace($"GenerateHousesOperator.fragmentOperatorApply(): createHouseSubGeo(): Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
                        }
#if false
                        try
                        {
                            createNeonSignsSubGeo(allEnv, worldFragment, fragPoints, height, neonMol, neonG);
                        }
                        catch (unknown: Dynamic ) {
                            trace('GenerateHousesOperator.fragmentOperatorApply(): createNeonSignsSubGeo(): Unknown exception applying fragment operator "${fragmentOperatorGetPath()}": '
                                + Std.string(unknown) + "\n"
                                + haxe.CallStack.toString(haxe.CallStack.callStack()));
                        }
#endif
                        haveHouse = true;
                    }

                }

            }

            if (g.IsEmpty())
            {
                return;
            }

            try
            {
                // var mol = new engine.SimpleMolecule( [g] );
                // TXWTODO: This is too inefficient. We should also use a factory here.
                engine.joyce.InstanceDesc instanceDesc = new();
                instanceDesc.Meshes.Add(g);
                instanceDesc.MeshMaterials.Add(0);
                instanceDesc.Materials.Add(_getHouseMaterial());
                worldFragment.AddStaticMolecule(instanceDesc);
            }
            catch (Exception e)
            {
                trace($"Unknown exception: {e}");
            }
            // worldFragment.AddStaticMolecule(neonMol);
        }
    

        public GenerateHousesOperator(
            engine.world.ClusterDesc clusterDesc,
            string strKey)
        {
            _clusterDesc = clusterDesc;
            _myKey = strKey;
            _rnd = new engine.RandomSource(strKey);
        }
    }
}
