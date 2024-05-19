using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Principal;
using System.Threading.Tasks;
using BepuPhysics;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.joyce;
using engine.physics;
using engine.world;
using nogame.modules.playerhover;
using static engine.Logger; 

namespace nogame.cities;

/**
 * Create the list of polytopes and the 3d geometry.
 */
public class GeneratePolytopeOperator : IFragmentOperator
{
    private class Context
    {
        public builtin.tools.RandomSource Rnd;
        public engine.world.Fragment Fragment;
    }
    
    private static object _classLock = new();
    private engine.world.ClusterDesc _clusterDesc;
    private string _myKey;
    private ShapeFactory _shapeFactory = I.Get<ShapeFactory>();
    

    public string FragmentOperatorGetPath()
    {
        return $"8012/GeneratePolytopeOperator/{_myKey}/{_clusterDesc.IdString}";
    }


    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    private async void _placePolytope(Context ctx, engine.streets.Estate estate)
    {
        Fragment worldFragment = ctx.Fragment;
        engine.physics.API aPhysics = engine.I.Get<engine.physics.API>();
        
        /*
         * We need to create two instances, one for the stand and one for the ball.
         * The stand will be static, the ball will not be, as it can be consumed.
         */
        Model modelStand = await I.Get<ModelCache>().Instantiate(
            $"polytope-stand-only.obj", new builtin.loader.ModelProperties(), new InstantiateModelParams()
            {
                GeomFlags = 0
                            | InstantiateModelParams.CENTER_X
                            | InstantiateModelParams.CENTER_Z
                            ,
                MaxDistance = 800f
            });
        var vPos =
            (_clusterDesc.Pos - worldFragment.Position +
            estate.GetCenter()) with { Y = _clusterDesc.AverageHeight + 2.5f };
        worldFragment.AddStaticInstance(
            0x00000001,
            "nogame.furniture.polytopeStand", modelStand.RootNode.InstanceDesc,
                vPos, Quaternion.Identity, null);
        // Trace($"in frag {worldFragment.GetId()} Placing polytope @{worldFragment.Position+vPos}");
        

        Model modelBall = await I.Get<ModelCache>().Instantiate(
            $"polytope-ball-only.obj", new builtin.loader.ModelProperties(), new InstantiateModelParams()
            {
                GeomFlags = 0
                            | InstantiateModelParams.CENTER_X
                            | InstantiateModelParams.CENTER_Z
                            | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC
                //| InstantiateModelParams.ROTATE_Y180
                ,
                MaxDistance = 800f
            });

        /*
         * Now, a bit more work for the ball, which is a dynamic entity that can
         * vanish. 
         */
        var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
        {
            var jInstanceDesc = modelBall.RootNode.InstanceDesc;
            eTarget.Set(new engine.world.components.FragmentId(worldFragment.NumericalId));
            eTarget.Set(new engine.joyce.components.Instance3(modelBall.RootNode.InstanceDesc));
            
            //eTarget.Set(new engine.audio.components.MovingSound(
            //    _getCar3Sound(carIdx), 150f));

            engine.joyce.components.Transform3 cTransform3 = new(
                true, 0x00800001, Quaternion.Identity, worldFragment.Position+vPos);
            eTarget.Set(cTransform3);
            engine.joyce.TransformApi.CreateTransform3ToParent(cTransform3, out var mat);
            eTarget.Set(new engine.joyce.components.Transform3ToParent(cTransform3.IsVisible, cTransform3.CameraMask, mat));

            BodyReference prefSphere;
            engine.physics.Object po;
            lock (worldFragment.Engine.Simulation)
            {
                po = new(worldFragment.Engine, eTarget,
                    _shapeFactory.GetSphereShape(jInstanceDesc.AABBTransformed.Radius))
                {
                    CollisionProperties = new engine.physics.CollisionProperties
                    { 
                        Entity = eTarget,
                        Name = "nogame.furniture.polytopeBall",
                        Flags = CollisionProperties.CollisionFlags.IsTangible | CollisionProperties.CollisionFlags.IsDetectable
                    }
                };
                prefSphere = worldFragment.Engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
            }
            eTarget.Set(new engine.physics.components.Body(po, prefSphere));
            eTarget.Set(new engine.draw.components.OSDText(
                new Vector2(0, -30f),
                new Vector2(160f, 18f),
                $"polytope {worldFragment.NumericalId}",
                12,
                0x88226622,
                0x00000000,
                engine.draw.HAlign.Left) {MaxDistance = 800f});
            var jFountainCubesInstanceDesc = InstanceDesc.CreateFromMatMesh(
                new MatMesh(
                    I.Get<ObjectRegistry<Material>>().Get("nogame.characters.polytope.materials.cube"),
                    engine.joyce.mesh.Tools.CreateCubeMesh("polytopefountain", 0.2f)
                ), 150f
            );
            eTarget.Set(new engine.behave.components.ParticleEmitter()
            {
                Position = new Vector3(0f, 2f, 0f),
                ScalePerSec = 1f,
                RandomPos = Vector3.One,
                EmitterTimeToLive = Int32.MaxValue,
                Velocity = 2f * Vector3.UnitY,
                ParticleTimeToLive = 60*2,
                InstanceDesc = jFountainCubesInstanceDesc,
                MaxDistance = 150f,
                CameraMask = 0x00000001,
            });

        });
        worldFragment.Engine.QueueEntitySetupAction("nogame.furniture.polytopeBall", tSetupEntity);
    }
    

    public Func<Task> FragmentOperatorApply(engine.world.Fragment worldFragment, FragmentVisibility visib) => new (async () =>
    {
        if (0 == (visib.How & FragmentVisibility.Visible3dAny))
        {
            return;
        }

        var ctx = new Context()
        {
            Rnd = new RandomSource(_myKey),
            Fragment = worldFragment
        };
 
        float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
        float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

        List<engine.streets.Estate> potentialEstates = new();
        
        /*
         * Iterate through all quarters in the clusters and generate lots and houses.
         */
        var quarterStore = _clusterDesc.QuarterStore();


        foreach (var quarter in quarterStore.GetQuarters())
        {
            if (quarter.IsInvalid())
            {
                Trace("Skipping invalid quarter.");
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
                 * Only consider this estate, if the center coordinate 
                 * is within this fragment.
                 */
                var center = estate.GetCenter();
                center.X += cx;
                center.Z += cz;
                if (!worldFragment.IsInsideLocal(center.X, center.Z))
                {
                    continue;
                }

                /*
                 * Polytope only can be done when no buildings are on top.
                 */
                var buildings = estate.GetBuildings();
                if (buildings.Count > 0)
                {
                    continue;
                }

                potentialEstates.Add(estate);
                
                //_placePolytope(worldFragment, estate);
            }
        }

        int nEstates = potentialEstates.Count;
        if (0 == nEstates)
        {
            return;
        }

        int idx = (int)(ctx.Rnd.GetFloat() * nEstates);
        var polytopeEstate = potentialEstates[idx];
        _placePolytope(ctx, polytopeEstate);

    });

    
    public GeneratePolytopeOperator(
        engine.world.ClusterDesc clusterDesc,
        string strKey
    ) {
        I.Get<ObjectRegistry<Material>>().RegisterFactory("nogame.characters.polytope.materials.cube",
            name => new Material()
            {
                Texture = I.Get<TextureCatalogue>().FindColorTexture(0xff226666),
            });
        _clusterDesc = clusterDesc;
        _myKey = strKey;
    }
    
    
    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GeneratePolytopeOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}