using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using builtin.tools.Lindenmayer;
using engine;
using engine.elevation;
using engine.joyce;
using engine.physics;
using static engine.Logger;
using static builtin.extensions.JsonObjectNumerics;

namespace nogame.cities;


/**
 * Create the 3d geometry for houses.
 */
public class GenerateHousesOperator : engine.world.IFragmentOperator
{
    private class Context
    {
        public engine.world.Fragment Fragment;
        public builtin.tools.RandomSource Rnd;
    }
    
    private engine.world.ClusterDesc _clusterDesc;
    private string _myKey;

    /*
     * Relation between basic texture and stories.
     * We do assume that a texture contains a integer number of stories.
     */
    private static float _storiesPerTexture = 8f;
    private static float _storyHeight = 3f;
    private static float _metersPerTexture = 3f * _storiesPerTexture;

    public bool TraceHouses { get; set; } = false; 
    
    public static HashSet<Vector2> SetDebugBuildings = new()
    {
        // new(-348f, -495f), // HPUCK
        // DELL notebook
        new(98f,-469f ),
        // DELL: for building with <347.42877, 39.856236, -225.3> center <359.07877, 39.856236, -192.325> :
        new Vector2 (-348f, -226f),
        // DELL: Trace: for building with <401.22876, 39.856236, 183.2> center <391.82877, 39.856236, 239.63335> :
        new Vector2( 401f,183f),
        // DELL: <343.22876, 39.856236, 275.2> center <342.75375, 39.856236, 304.575> : 0 up, 4 down.
        new Vector2(343f, 275f),
        // DELL: building with <91.62877, 39.856236, 189.1> center <74.27877, 39.856236, 223.07501> : 0 up, 4 down.
        new Vector2(91f, 189f)
    };

    public string FragmentOperatorGetPath()
    {
        return $"8001/GenerateHousesOperator/{_myKey}/{_clusterDesc.IdString}";
    }


    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }
    
    private bool _isDebugBuilding(in Vector3 p0)
    {  
        bool isDebugBuilding = SetDebugBuildings.Contains(new Vector2(Single.Floor(p0.X), Single.Floor(p0.Z)));
        return isDebugBuilding;
    }


    private void _breakOnDebugBuilding(in Vector3 p0)
    {
        bool isDebugBuilding = _isDebugBuilding(p0);
        if (isDebugBuilding)
        {
            int a = 1;
        }
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
        in Context ctx,
        in engine.joyce.MatMesh matmesh,
        in IList<Vector3> p,
        float h0, float mpt,
        in IList<Func<IList<StaticHandle>, Action>> listCreatePhysics
    )
    {
        var worldFragment = ctx.Fragment;
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
            // listWalls.Add(p[(i + 1) % l]);
        }
        
        /*
         * Debug test: Is the normal facing the right direction? Is it convex at all?
         */
        if (true)
        {
            int nUp = 0, nDown = 0;
            Vector3 v3Center = Vector3.Zero;
            for (int i = 0; i < l; ++i)
            {
                v3Center += p[i];
                Vector3 v3a = p[(i + 0) % l] - p[(i + 1) % l];
                Vector3 v3b = p[(i + 2) % l] - p[(i + 1) % l];

                Vector3 v3Normal = Vector3.Cross(v3a, v3b);
                if (v3Normal.Y > 0.1f) nUp++;
                else if (v3Normal.Y < -0.1f) nDown++;
            }

            v3Center /= l;
            if (TraceHouses) Trace($"f {worldFragment.IdxFragment} b {p[0]+worldFragment.Position} c {v3Center+worldFragment.Position} h {h0}: {nUp} u, {nDown} d.");
            _breakOnDebugBuilding((p[0] + worldFragment.Position));
        }
        
        /*
         * 27 is the magical number we currently use to identify buildings in collisions.
         */
        var opExtrudePoly = new builtin.tools.ExtrudePoly(listWalls, path, 27, _metersPerTexture, false, false, true)
        {
            PairedNormals = true,
            TileToTexture = true
        };
        try
        {
            opExtrudePoly.BuildGeom(meshHouse);
            //{
            //    _breakOnDebugBuilding(p[0]+worldFragment.Position);
            //}
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
        in Context ctx,
        in engine.joyce.MatMesh matmesh,
        in IList<Vector3> fragPoints,
        float height)
    {
        var worldFragment = ctx.Fragment;
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

        if (ctx.Rnd.GetFloat() < 0.7f)
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
            + ctx.Rnd.GetFloat() * (diff.Length() - 15f) * vUnitSide * _storyHeight
            + vUnitOut,
            vUnitSide * 15f,
            vUnitUp * 60f,
            Vector2.Zero,
            Vector2.UnitY,
            Vector2.UnitX
        );

        int adIdx = (int)(1f + ctx.Rnd.GetFloat() * 1.99f);

        matmesh.Add(I.Get<ObjectRegistry<Material>>().Get($"nogame.cities.houses.material.ad{adIdx}"), mesh);

    }


    private void _createShopFrontsSubGeo(
        in Context ctx,
        in Vector3 vOffset,
        in engine.joyce.MatMesh matmesh,
        in engine.streets.ShopFront shopFront)
    {
        var worldFragment = ctx.Fragment;
        string? materialName = null;
        if (shopFront.Tags.Contains("shop Game2"))
        {
            materialName = "nogame.cities.houses.material.fishmongers-window";
        }
        else if (shopFront.Tags.Contains("shop Drink"))
        {
            materialName = "nogame.cities.houses.material.drink-window";
        }
        else if (shopFront.Tags.Contains("shop Eat"))
        {
            materialName = "nogame.cities.houses.material.eat-window";
        }
        else
        {
            materialName = "nogame.cities.houses.material.empty-window";
        }

        engine.joyce.Material materialShopFront = I.Get<ObjectRegistry<Material>>().Get(materialName);
        engine.joyce.Mesh meshShopFront = new($"{worldFragment.GetId()}-shopfrontsubgeo");

        var p = shopFront.GetPoints();
        var vUp = Vector3.UnitY * (_storyHeight-0.15f);
        var vGround = Vector3.UnitY * 2.05f;
        int l = p.Count;
        for (int i = 1; i < l; ++i)
        {
            engine.joyce.mesh.Tools.AddQuadXYUV(
                meshShopFront, vGround + vOffset + p[i-1], p[i] - p[i-1], vUp,
                Vector2.UnitY, Vector2.UnitX, -Vector2.UnitY
            );
            matmesh.Add(materialShopFront, meshShopFront);
        }
    }
    

    private void _createNeonSignSubGeo(
        in Context ctx,
        in engine.joyce.MatMesh matmesh,
        in Vector3 p0, in Vector3 pe,
        float h)
    {
        var worldFragment = ctx.Fragment;
        engine.joyce.Material materialNeon = I.Get<ObjectRegistry<Material>>().Get("nogame.cities.houses.materials.neon");
        var meshNeon = engine.joyce.Mesh.CreateListInstance($"{worldFragment.GetId()}-neonsignsubgeo");

        /*
         * Number of letters.
         */
        int nLetters = 2 + (int)(ctx.Rnd.GetFloat() * 8.0);

        float letterHeight = 1.5f;

        /*
         * height of first letter.
         */
        float h0 = ctx.Rnd.GetFloat() * (h - nLetters * letterHeight - 3.0f);
        float h1 = h0 + nLetters * letterHeight;

        for (int i = 0; i < nLetters; i++)
        {
            int lx = ctx.Rnd.Get8() & 15;
            int ly = ctx.Rnd.Get8() & 3;
            float y = p0.Y + h0 + i * letterHeight;
            engine.joyce.mesh.Tools.AddQuadXYUV(meshNeon,
                p0 with { Y=y },
                pe,
                Vector3.UnitY * letterHeight,
                new Vector2(lx/16f,ly/4f + 1f/4f),
                new Vector2(1f/16f, 0f),
                new Vector2(0f, -1f/4f)
            );
        }

        matmesh.Add(materialNeon, meshNeon);
    }


    /**
     * Create large-scale neon-lights for the given house geometry.
     */
    private void _createNeonSignsSubGeo(
        in Context ctx,
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
                ctx, matmesh,
                p0, pe, h);
        }
    }



    public Func<Task> FragmentOperatorApply(engine.world.Fragment worldFragment, engine.world.FragmentVisibility visib) => new (async () =>
    {
        Context ctx = new()
        {
            Rnd = new builtin.tools.RandomSource(_myKey),
            Fragment = worldFragment
        };
        

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
        
        if (TraceHouses) Trace($"frag {worldFragment.Position} aabb {worldFragment.AABB}");
        
        // TXWTODO: I'd love to have a better thing than this.
        List<Func<IList<StaticHandle>, Action>> listCreatePhysics = new();

        /*
         * This is where we create our houses in.
         */
        engine.joyce.MatMesh matmesh = new();
        bool haveDebugBuilding = false;

        /*
         * Iterate through all quarters in the clusters and generate lots and houses.
         */
        var quarterStore = _clusterDesc.QuarterStore();
        bool isFirst = false;

        foreach (var quarter in quarterStore.GetQuarters())
        {
            if (quarter.IsInvalid())
            {
                Trace($"Skipping invalid quarter.");
                continue;
            }

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
                        continue;
                    }
                    else
                    {
                    }
                    
                    var orgPoints = building.GetPoints();
                    bool isDebugBuilding = _isDebugBuilding(orgPoints[0] + _clusterDesc.Pos);
                    haveDebugBuilding |= isDebugBuilding;
                    if (isDebugBuilding)
                    {
                        if (TraceHouses) Trace($"This is the debug building p0 = {orgPoints[0]+_clusterDesc.Pos} c = {orgCenter+_clusterDesc.Pos}");
                    }
                    if (!isFirst)
                    {
                        isFirst = true;
                        if (TraceHouses) Trace($"First run with building p0 {orgPoints[0]+_clusterDesc.Pos}");
                    }
                        
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
                        /*
                         * First, perform the new procedural way of crafting the building.
                         */
                        var gen = new HouseInstanceGenerator();
                        var lSystem = gen.CreateHouse1System(
                            new Params(
                                new JsonObject
                                {
                                    ["A"] = From(fragPoints),
                                    ["h"] = height
                                }
                            ),
                            ctx.Rnd
                        );
                        var lInstance = new LGenerator(lSystem).Generate(3);
                        new AlphaInterpreter(lInstance).Run(ctx.Fragment, Vector3.Zero, matmesh, listCreatePhysics);
                    }
                    catch (Exception e)
                    {
                        Error($"Unable to generate lindenmeyer houses: {e}");
                    }

                    try
                    {
#if false
                        _createClassicHouseSubGeo(
                            ctx, matmesh,
                            fragPoints, height, _metersPerTexture,
                            listCreatePhysics);
#endif

                        _createLargeAdvertsSubGeo(
                            ctx, matmesh, fragPoints, height); //15434
                    }
                    catch (Exception e)
                    {
                        Trace($"Unknown exception applying fragment operator '{FragmentOperatorGetPath()}': {e}");
                    }

                    try
                    {
                        _createNeonSignsSubGeo(ctx, matmesh,
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
                            _createShopFrontsSubGeo(ctx, vC, matmesh, shopFront);
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
            if (haveDebugBuilding)
            {
                int a = 1;
            }
            var mmmerged = MatMesh.CreateMerged(matmesh);
            var id = engine.joyce.InstanceDesc.CreateFromMatMesh(mmmerged, 1500f);
            worldFragment.AddStaticInstance($"nogame.cities.houses {worldFragment.Position}", id, listCreatePhysics);
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
                Texture = I.Get<TextureCatalogue>().FindTexture("buildingalphadiffuse.png"),
                AddInterior = true,
            });
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.materials.houses.win2",
            (name) => new engine.joyce.Material()
            {
                Texture = I.Get<TextureCatalogue>().FindTexture("buildingalphadiffuse2.png"),
                AddInterior = true,
            });
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.materials.houses.win3",
            (name) => new engine.joyce.Material()
            {
                Texture = I.Get<TextureCatalogue>().FindTexture("buildingalphadiffuse3.png"),
                AddInterior = true,
            });
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.materials.neon",
            (name) => new engine.joyce.Material()
            {
                Texture = null,
                EmissiveTexture = I.Get<TextureCatalogue>().FindTexture("lorem.png"),
                HasTransparency = true
            });

        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.material.ad1",
            name => new Material()
            {
                HasTransparency = true,
                EmissiveFactors = 0x77ffffff,
                EmissiveTexture = I.Get<TextureCatalogue>().FindTexture("sprouce-cn.png")
            });
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.material.ad2",
            name => new Material()
            {
                HasTransparency = true,
                EmissiveFactors = 0x77ffffff,
                EmissiveTexture = I.Get<TextureCatalogue>().FindTexture("plentomatic.png")
            });
        
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.material.drink-window",
            name => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindTexture("drink-window-albedo.png"),
                EmissiveTexture = I.Get<TextureCatalogue>().FindTexture("drink-window-emissive.png")
            });

        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.material.eat-window",
            name => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindTexture("eat-window-albedo.png"),
                EmissiveTexture = I.Get<TextureCatalogue>().FindTexture("eat-window-emissive.png"),
            });

        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.material.fishmongers-window",
            name => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindTexture("fishmongers-window-albedo.png"),
                EmissiveTexture = I.Get<TextureCatalogue>().FindTexture("fishmongers-window-emissive.png")
            });

        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.cities.houses.material.empty-window",
            name => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindTexture("empty-window-albedo.png")
            });

    }
    

    public GenerateHousesOperator(
        engine.world.ClusterDesc clusterDesc,
        string strKey)
    {
        _clusterDesc = clusterDesc;
        _myKey = strKey;

        _registerMaterials();
    }


    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GenerateHousesOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}
