﻿using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.elevation;
using engine.joyce;
using engine.physics;
using static engine.Logger;


namespace nogame.cities;


/**
 * Create the 3d geometry for houses.
 */
public class GenerateHousesOperator : engine.world.IFragmentOperator
{
    static private object _lo = new();

    private engine.world.ClusterDesc _clusterDesc;
    private builtin.tools.RandomSource _rnd;
    private string _myKey;

    /*
     * Relation between basic texture and stories.
     * We do assume that a texture contains a integer number of stories.
     */
    private static float _storiesPerTexture = 32f;
    private static float _storyHeight = 3f;
    private static float _metersPerTexture = 3f * _storiesPerTexture;


    public string FragmentOperatorGetPath()
    {
        return $"8001/GenerateHousesOperator/{_myKey}/{_clusterDesc.IdString}";
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
    private void _createClassicHouseSubGeo(
        in engine.world.Fragment worldFragment,
        in engine.joyce.MatMesh matmesh,
        in IList<Vector3> p,
        float h0, float mpt,
        in IList<Func<IList<StaticHandle>, Action>> listCreatePhysics
    )
    {
        if (p.Count < 1) return;
        uint matIdx = (((uint)(p[0].X + p[0].Z * 123.0)) % 3) + 1;
        engine.joyce.Material materialHouse = I.Get<ObjectRegistry<Material>>().Get($"nogame.cities.houses.materials.houses.win{matIdx}");
        engine.joyce.Mesh meshHouse = new($"{worldFragment.GetId()}-housessubgeo");



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
         * To generate real surfaces, we need to create a poly with consecutive pairs of vertices
         * that form a wall each.
         */
        List<Vector3> listWalls = new();
        int l = p.Count;
        for (int i = 0; i < l; ++i)
        {
            listWalls.Add(p[i]);
            listWalls.Add(p[(i + 1) % l]);
        }
        
        /*
         * Debug test: Is the normal facing the right direction?
         */
        if (false) {
            Vector3 v3RoofNormal = Vector3.Cross(p[0], p[1]);
            Trace($"roof normal: @{p[0]+worldFragment.Position} is {v3RoofNormal}");
        }
        
        /*
         * 27 is the magical number we currently use to identify buildings in collisions.
         */
        var opExtrudePoly = new builtin.tools.ExtrudePoly(listWalls, path, 27, _metersPerTexture, false, false, true)
        {
            PairedNormals = true
        };
        try
        {
            opExtrudePoly.BuildGeom(meshHouse);
            matmesh.Add(materialHouse, meshHouse);
        }
        catch (Exception e)
        {
            Trace($"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
        }

        CollisionProperties props = new(){
            Flags = 
                CollisionProperties.CollisionFlags.IsTangible 
                | CollisionProperties.CollisionFlags.IsDetectable,
            Name = $"house-{p[0]+worldFragment.Position}",
        };
        try
        {
            var fCreatePhysics = opExtrudePoly.BuildStaticPhys(worldFragment, props);
            listCreatePhysics.Add(fCreatePhysics);
        }
        catch (Exception e)
        {
            Trace($"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
        }
    }


    /**
     * Look for a suitably high wall and place an advert on it.
     */
    private void _createLargeAdvertsSubGeo(
        in engine.world.Fragment worldFragment,
        in engine.joyce.MatMesh matmesh,
        in IList<Vector3> fragPoints,
        float height)
    {
        /*
         * Let's assume the ads are 10m in height
         */
        if (height < 75f)
        {
            return;
        }

        float minWidth = 20f;
        /*
         * now find a reasonably wide wall.
         */
        int np = fragPoints.Count;
        int idx = -1;
        Vector3 diff = new();
        for (int i = 0; i < np; ++i)
        {
            diff = fragPoints[(i + 1) % np] - fragPoints[i];
            // TXWTODO: We always take the first fitting wall.
            if (diff.LengthSquared() >= minWidth * minWidth)
            {
                idx = i;
                break;
            }
        }

        if (idx == -1)
        {
            return;
        }

        if (_rnd.GetFloat() < 0.7f)
        {
            return;
        }

        Vector3 vOut = new(-diff.Z, 0f, diff.X);
        Vector3 vUnitOut = vOut / vOut.Length();
        Vector3 vUnitSide = diff / diff.Length();
        Vector3 vUnitUp = Vector3.UnitY;


        engine.joyce.Mesh mesh = new($"{worldFragment.GetId()}-largeadverts");
        engine.joyce.mesh.Tools.AddQuadXYUV(
            mesh,
            fragPoints[idx]
            + Single.Min(15f, height - 2f - 60f) * vUnitUp
            + _rnd.GetFloat() * (diff.Length() - 15f) * vUnitSide * _storyHeight
            + vUnitOut,
            vUnitSide * 15f,
            vUnitUp * 60f,
            Vector2.Zero,
            Vector2.UnitY,
            Vector2.UnitX
        );

        int adIdx = (int)(1f + _rnd.GetFloat() * 1.99f);

        matmesh.Add(I.Get<ObjectRegistry<Material>>().Get($"nogame.cities.houses.material.ad{adIdx}"), mesh);

    }


    private void _createShopFrontsSubGeo(
        in engine.world.Fragment worldFragment,
        in Vector3 vOffset,
        in engine.joyce.MatMesh matmesh,
        in engine.streets.ShopFront shopFront)
    {
        engine.joyce.Material materialShopFront = I.Get<ObjectRegistry<Material>>().Get("nogame.cities.houses.material.ad1");
        engine.joyce.Mesh meshShopFront = new($"{worldFragment.GetId()}-shopfrontsubgeo");

        var p = shopFront.GetPoints();
        var vUp = Vector3.UnitY * (_storyHeight-0.15f);
        var vGround = Vector3.UnitY * 2.05f;
        int l = p.Count;
        for (int i = 1; i < l; ++i)
        {
            engine.joyce.mesh.Tools.AddQuadXYUV(
                meshShopFront, vGround + vOffset + p[i-1], p[i] - p[i-1], vUp, Vector2.Zero, Vector2.UnitX, Vector2.UnitY
            );
            matmesh.Add(materialShopFront, meshShopFront);
        }
    }
    

    private void _createNeonSignSubGeo(
        in engine.world.Fragment worldFragment,
        in engine.joyce.MatMesh matmesh,
        in Vector3 p0, in Vector3 pe,
        float h)
    {
        engine.joyce.Material materialNeon = I.Get<ObjectRegistry<Material>>().Get("nogame.cities.houses.materials.neon");
        engine.joyce.Mesh meshNeon = new($"{worldFragment.GetId()}-neonsignsubgeo");

        /*
         * Number of letters.
         */
        int nLetters = 2 + (int)(_rnd.GetFloat() * 8.0);

        float letterHeight = 1.5f;

        /*
         * height of first letter.
         */
        float h0 = _rnd.GetFloat() * (h - nLetters * letterHeight - 3.0f);
        float h1 = h0 + nLetters * letterHeight;

        /*
         * Trivial implementation: Add a part of the texture, which is 8x8
         */
        uint i0 = meshNeon.GetNextVertexIndex();

        meshNeon.p(p0.X, p0.Y + h0, p0.Z);
        meshNeon.UV(0.0f, 1.0f - 0.0f);
        meshNeon.p(p0.X, p0.Y + h1, p0.Z);
        meshNeon.UV(0.0f, 1.0f - 0.125f * nLetters);
        meshNeon.p(p0.X + pe.X, p0.Y + h1 + pe.Y, p0.Z + pe.Z);
        meshNeon.UV(0.125f, 1.0f - 0.125f * nLetters);
        meshNeon.p(p0.X + pe.X, p0.Y + h0 + pe.Y, p0.Z + pe.Z);
        meshNeon.UV(0.125f, 1.0f - 0.0f);

        meshNeon.Idx(i0 + 0, i0 + 1, i0 + 3);
        meshNeon.Idx(i0 + 1, i0 + 2, i0 + 3);

        matmesh.Add(materialNeon, meshNeon);
    }


    /**
     * Create large-scale neon-lights for the given house geometry.
     */
    private void _createNeonSignsSubGeo(
        in engine.world.Fragment worldFragment,
        in engine.joyce.MatMesh matmesh,
        in IList<Vector3> p,
        float h)
    {
        if (h < 2.9f * _storyHeight)
        {
            return;
        }
        
        /*
         * For the neon sign, we each of the corner points, using 1 meter in wall direction to
         * outside to place the rectangle.
         */

        float letterWidth = 1.5f;

        var l = p.Count;
        for (int i = 0; i < l; i++)
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
                worldFragment, matmesh,
                p0, pe, h);
        }
    }



    public Func<Task> FragmentOperatorApply(engine.world.Fragment worldFragment, engine.world.FragmentVisibility visib) => new (async () =>
    {
        if (0 == (visib.How & engine.world.FragmentVisibility.Visible3dAny))
        {
            return;
        }

        Vector3 vC = (_clusterDesc.Pos - worldFragment.Position) with { Y = _clusterDesc.AverageHeight };
        
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
        _rnd.Clear();

        // TXWTODO: I'd love to have a better thing than this.
        List<Func<IList<StaticHandle>, Action>> listCreatePhysics = new();

        /*
         * This is where we create our houses in.
         */
        engine.joyce.MatMesh matmesh = new();

        /*
         * Iterate through all quarters in the clusters and generate lots and houses.
         */
        var quarterStore = _clusterDesc.QuarterStore();

        foreach (var quarter in quarterStore.GetQuarters())
        {
            if (quarter.IsInvalid())
            {
                Trace($"Skipping invalid quarter.");
                continue;
            }

            /*
             * Place on house in each quarter in the middle.
             */
            float xmiddle = 0.0f;
            float ymiddle = 0.0f;
            int n = 0;
            var delims = quarter.GetDelims();
            foreach (var delim in delims)
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
            foreach (var estate in quarter.GetEstates())
            {

                /*
                 * Now create a house subgeometry for each of the buildings on the
                 * estate.
                 */
                foreach (var building in estate.GetBuildings())
                {
                    var orgCenter = building.GetCenter();
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
                    foreach (var p in orgPoints)
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
                        _createClassicHouseSubGeo(
                            worldFragment, matmesh,
                            fragPoints, height, _metersPerTexture,
                            listCreatePhysics);

                        _createLargeAdvertsSubGeo(
                            worldFragment, matmesh, fragPoints, height);
                    }
                    catch (Exception e)
                    {
                        Trace($"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
                    }

                    try
                    {
                        _createNeonSignsSubGeo(worldFragment, matmesh,
                            fragPoints, height);
                    }
                    catch (Exception e)
                    {
                        Trace($"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
                    }

                    foreach (var shopFront in building.GetShopFronts())
                    {
                        try
                        {
                            _createShopFrontsSubGeo(worldFragment, vC, matmesh, shopFront);
                        }
                        catch (Exception e)
                        {
                            Trace($"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
                        }
                    }
                }
            }

        }

        if (matmesh.IsEmpty())
        {
            return;
        }

        try
        {
            // TXWTODO: Merge this, this is inefficient.
            var mmmerged = MatMesh.CreateMerged(matmesh);
            var id = engine.joyce.InstanceDesc.CreateFromMatMesh(mmmerged, 1500f);
            worldFragment.AddStaticInstance("nogame.cities.houses", id, listCreatePhysics);
        }
        catch (Exception e)
        {
            Trace($"Unknown exception: {e}");
        }
    });


    private void _registerMaterials()
    {
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.materials.houses.win1",
            (name) => new engine.joyce.Material()
            {
                AlbedoColor = (bool)engine.Props.Get("debug.options.flatshading", false) != true
                    ? 0x00000000
                    : 0xff333333,
                Texture = new engine.joyce.Texture("buildingalphadiffuse.png"),
                AddInterior = true,
            });
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.materials.houses.win2",
            (name) => new engine.joyce.Material()
            {
                AlbedoColor = (bool)engine.Props.Get("debug.options.flatshading", false) != true
                    ? 0x00000000
                    : 0xff333333,
                Texture = new engine.joyce.Texture("buildingalphadiffuse2.png"),
                AddInterior = true,
            });
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.materials.houses.win3",
            (name) => new engine.joyce.Material()
            {
                AlbedoColor = (bool)engine.Props.Get("debug.options.flatshading", false) != true
                    ? 0x00000000
                    : 0xff333333,
                Texture = new engine.joyce.Texture("buildingalphadiffuse3.png"),
                AddInterior = true,
            });
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.materials.neon",
            (name) => new engine.joyce.Material()
            {
                AlbedoColor = (bool)engine.Props.Get("debug.options.flatshading", false) != true
                    ? 0x00000000
                    : 0xffff3333,
                Texture = null,
                EmissiveTexture = new engine.joyce.Texture("lorem.png"),
                HasTransparency = true
            });

        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.material.ad1",
            name => new Material()
            {
                HasTransparency = true,
                EmissiveFactors = 0x77ffffff,
                EmissiveTexture = new engine.joyce.Texture("sprouce-cn.png")
            });
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.material.ad2",
            name => new Material()
            {
                HasTransparency = true,
                EmissiveFactors = 0x77ffffff,
                EmissiveTexture = new engine.joyce.Texture("plentomatic.png")
            });
    }
    

    public GenerateHousesOperator(
        engine.world.ClusterDesc clusterDesc,
        string strKey)
    {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
        _rnd = new builtin.tools.RandomSource(strKey);

        _registerMaterials();
    }


    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GenerateHousesOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}
