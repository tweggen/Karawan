﻿using BepuPhysics;
using BepuPhysics.Collidables;
using DefaultEcs;
using System;
using System.Collections.Generic;
using System.Numerics;
using static engine.Logger;


namespace nogame.cities
{
    public class GenerateHousesOperator : engine.world.IFragmentOperator
    {
        static private object _lo = new();
        static private engine.joyce.Material _jMaterialHouse = null;
        static private engine.joyce.Material _jMaterialNeon = null;

        static private engine.joyce.Material _getHouseMaterial()
        {
            lock (_lo)
            {
                if (_jMaterialHouse == null)
                {
                    _jMaterialHouse = new engine.joyce.Material(); 
                    _jMaterialHouse.AlbedoColor = engine.GlobalSettings.Get("debug.options.flatshading") != "true"
                        ? 0x00000000 : 0xff333333;
                    _jMaterialHouse.Texture = new engine.joyce.Texture("buildingdiffuse.png");
                }
                return _jMaterialHouse;
            }
        }

        static private engine.joyce.Material _getNeonMaterial()
        {
            lock (_lo)
            {
                if (_jMaterialNeon == null)
                {
                    _jMaterialNeon = new engine.joyce.Material();
                    _jMaterialNeon.AlbedoColor = engine.GlobalSettings.Get("debug.options.flatshading") != "true"
                        ? 0xffff3333 : 0xffff3333;
                    _jMaterialNeon.Texture = null;
                    _jMaterialNeon.EmissiveTexture = new engine.joyce.Texture("lorem.png");
                    _jMaterialNeon.HasTransparency = true;
                }
                return _jMaterialNeon;
            }
        }

        private engine.world.ClusterDesc _clusterDesc;
        private engine.RandomSource _rnd;
        private string _myKey;

        /*
         * Relation between basic texture and stories.
         * We do assume that a texture contains a integer number of stories.
         */
        private static float _storiesPerTexture = 32f;
        private static float _metersPerTexture = 3.0f * _storiesPerTexture;

        public string FragmentOperatorGetPath()
        {
            return $"8001/GenerateHousesOperator/{_myKey}/";
        }


        public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
        {
            _clusterDesc.GetAABB(out aabb);
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
            in engine.joyce.Mesh g,
            in IList<Func<IList<StaticHandle>, Action>> listCreatePhysics
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
                opExtrudePoly.BuildGeom( g);
            } catch (Exception e) {
                Trace( $"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
            }
            
            try
            {
                var fCreatePhysics = opExtrudePoly.BuildStaticPhys(worldFragment);
                listCreatePhysics.Add(fCreatePhysics);
            } catch (Exception e) {
                Trace( $"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
            }

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
                catch (Exception) { }
            }
            return inFragmentY;
        }


        private void _createNeonSignSubGeo(
            in engine.world.Fragment worldFragment,
            in Vector3 p0, in Vector3 pe,
            float h,
            in engine.joyce.Mesh neonG)
        {
            /*
             * Number of letters.
             */
            int nLetters = 2 + (int)(_rnd.getFloat() * 8.0);

            float letterHeight = 1.5f;

            /*
             * height of first letter.
             */
            float h0 = _rnd.getFloat() * (h - nLetters * letterHeight - 3.0f);
            float h1 = h0 + nLetters * letterHeight;

            /*
             * Trivial implementation: Add a part of the texture, which is 8x8
             */
            uint i0 = neonG.GetNextVertexIndex();

            neonG.p(p0.X, p0.Y + h0, p0.Z);
            neonG.UV(0.0f, 1.0f - 0.0f);
            neonG.p(p0.X, p0.Y + h1, p0.Z);
            neonG.UV(0.0f, 1.0f - 0.125f * nLetters);
            neonG.p(p0.X + pe.X, p0.Y + h1 + pe.Y, p0.Z + pe.Z);
            neonG.UV(0.125f, 1.0f - 0.125f * nLetters);
            neonG.p(p0.X + pe.X, p0.Y + h0 + pe.Y, p0.Z + pe.Z);
            neonG.UV(0.125f, 1.0f - 0.0f);

            neonG.Idx(i0 + 0, i0 + 1, i0 + 3);
            neonG.Idx(i0 + 1, i0 + 2, i0 + 3);
        }


        /**
         * Create large-scale neon-lights for the given house geometry.
         */
        private void _createNeonSignsSubGeo(
                in engine.world.Fragment worldFragment,
                in IList<Vector3> p,
                float h,
                engine.joyce.Mesh neonG)
        {
            /*
             * For the neon sign, we each of the corner points, using 1 meter in wall direction to
             * outside to place the rectangle.
             */

            float letterWidth = 1.5f;

            var l = p.Count;
            for (int i=0; i<l; i++ )
            {
                /*
                 * Start point of sign
                 */
                Vector3 p0 = p[i];
                /*
                 * Extent of sign.
                 */
                Vector3 pe = p[(i + 1) % l];

                pe -= p0;
                pe = Vector3.Normalize(pe);
                pe *= -letterWidth;

                _createNeonSignSubGeo(
                    worldFragment,
                    p0, pe,
                    h,
                    neonG);
            }
        }



        public void FragmentOperatorApply(
            in engine.world.Fragment worldFragment)
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

            engine.joyce.Mesh g = engine.joyce.Mesh.CreateListInstance();
            engine.joyce.Mesh neonG = engine.joyce.Mesh.CreateListInstance();
            List<Func<IList<StaticHandle>, Action>> listCreatePhysics = new();

            /*
             * Iterate through all quarters in the clusters and generate lots and houses.
             */
            var quarterStore = _clusterDesc.QuarterStore();

            foreach (var quarter in quarterStore.GetQuarters() )
            {
                if (quarter.IsInvalid())
                {
                    Trace( $"Skipping invalid quarter.");
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
                                worldFragment, fragPoints, height, _metersPerTexture,
                                g, listCreatePhysics);
                        }
                        catch (Exception e) {
                            Trace($"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
                        }
                        try
                        {
                            _createNeonSignsSubGeo(worldFragment, fragPoints, height, neonG);
                        }
                        catch (Exception e) {
                            Trace($"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
                        }

                        //haveHouse = true;
                    }

                }

            }

            if (g.IsEmpty())
            {
                return;
            }

            // TXWTODO: We currently split this into two different molecules (because there's just one RlMeshEntry per entity). I'd like this to be the same.

            try
            {
                // var mol = new engine.SimpleMolecule( [g] );
                // TXWTODO: This is too inefficient. We should also use a factory here.
                {
                    engine.joyce.InstanceDesc instanceDesc = new();
                    instanceDesc.Meshes.Add(g);
                    instanceDesc.MeshMaterials.Add(0);
                    instanceDesc.Materials.Add(_getHouseMaterial());
                    worldFragment.AddStaticMolecule("nogame.cities.houses.walls", instanceDesc, listCreatePhysics);
                }
                {
                    engine.joyce.InstanceDesc instanceDesc = new();
                    instanceDesc.Meshes.Add(neonG);
                    instanceDesc.MeshMaterials.Add(0);
                    instanceDesc.Materials.Add(_getNeonMaterial());
                    worldFragment.AddStaticMolecule("nogame.cities.houses.neon", instanceDesc);
                }
            }
            catch (Exception e)
            {
                Trace($"Unknown exception: {e}");
            }
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
