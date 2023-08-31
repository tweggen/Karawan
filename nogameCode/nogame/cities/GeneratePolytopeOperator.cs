using System;
using System.Collections.Generic;
using System.Numerics;
using System.Security.Principal;
using System.Threading.Tasks;
using BepuPhysics;
using engine;
using engine.joyce;
using engine.world;
using static engine.Logger; 

namespace nogame.cities;

public class GeneratePolytopeOperator : IFragmentOperator
{
    private static object _classLock = new();
    private engine.world.ClusterDesc _clusterDesc;
    private engine.RandomSource _rnd;
    private string _myKey;
    
    private static SortedDictionary<float, BepuPhysics.Collidables.TypedIndex> _mapPshapeSphere = new();
    private static SortedDictionary<float, BepuPhysics.Collidables.Sphere> _mapPbodySphere = new();
    private static BepuPhysics.Collidables.TypedIndex _getSphereShape(float radius, in Engine engine)
    {
        lock(_classLock)
        {
            BepuPhysics.Collidables.TypedIndex pshapeSphere;
            if (_mapPshapeSphere.TryGetValue(radius, out pshapeSphere))
            {
                return pshapeSphere;
            }

            BepuPhysics.Collidables.Sphere pbodySphere = new(radius); 
            lock (engine.Simulation)
            {
                pshapeSphere = engine.Simulation.Shapes.Add(pbodySphere);
            }

            _mapPbodySphere[radius] = pbodySphere;
            _mapPshapeSphere[radius] = pshapeSphere;
            
            return pshapeSphere;
        }
    }




    public string FragmentOperatorGetPath()
    {
        return $"8012/GeneratePolytopeOperator/{_myKey}/{_clusterDesc.Id}";
    }


    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    private async void _placePolytope(engine.world.Fragment worldFragment, engine.streets.Estate estate)
    {
        engine.physics.API aPhysics = engine.Implementations.Get<engine.physics.API>();
        
        /*
         * We need to create two instances, one for the stand and one for the ball.
         * The stand will be static, the ball will not be, as it can be consumed.
         */
        Model modelStand = await ModelCache.Instance().Instantiate(
            $"polytope-stand-only.obj", new builtin.loader.ModelProperties(), new InstantiateModelParams()
            {
                GeomFlags = 0
                            | InstantiateModelParams.CENTER_X
                            | InstantiateModelParams.CENTER_Z
                            ,
                MaxDistance = 800f
            });
        var vPos =
            _clusterDesc.Pos - worldFragment.Position +
            estate.GetCenter() with { Y = _clusterDesc.AverageHeight + 2.5f };
        worldFragment.AddStaticInstance(
            "nogame.furniture.polytopeStand", modelStand.InstanceDesc,
                vPos, Quaternion.Identity, null);
        Trace($"in frag {worldFragment.GetId()} Placing polytope @{worldFragment.Position+vPos}");
        

        Model modelBall = await ModelCache.Instance().Instantiate(
            $"polytope-ball-only.obj", new builtin.loader.ModelProperties(), new InstantiateModelParams()
            {
                GeomFlags = 0
                            | InstantiateModelParams.CENTER_X
                            | InstantiateModelParams.CENTER_Z
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
            ModelInfo modelInfo = modelBall.ModelInfo;
            
            eTarget.Set(new engine.world.components.FragmentId(worldFragment.NumericalId));
            eTarget.Set(new engine.joyce.components.Instance3(modelBall.InstanceDesc));
            
            //eTarget.Set(new engine.audio.components.MovingSound(
            //    _getCar3Sound(carIdx), 150f));

            engine.transform.components.Transform3 cTransform3 = new(
                true, 0x00000001, Quaternion.Identity, worldFragment.Position+vPos);
            eTarget.Set(cTransform3);
            engine.transform.API.CreateTransform3ToParent(cTransform3, out var mat);
            eTarget.Set(new engine.transform.components.Transform3ToParent(cTransform3.IsVisible, cTransform3.CameraMask, mat));
            

            BodyHandle phandleSphere = worldFragment.Engine.Simulation.Bodies.Add(
                BodyDescription.CreateKinematic(
                    new Vector3(0f, 0f, 0f), // infinite mass, this is a kinematic object.
                    new BepuPhysics.Collidables.CollidableDescription(
                        _getSphereShape(modelInfo.AABB.Radius, worldFragment.Engine),
                        0.1f),
                    new BodyActivityDescription(0.01f)
                )
            );
            BodyReference prefSphere = worldFragment.Engine.Simulation.Bodies.GetBodyReference(phandleSphere);
            engine.physics.CollisionProperties collisionProperties =
                new engine.physics.CollisionProperties
                    { Entity = eTarget, Name = "nogame.furniture.polytopeBall", IsTangible = true };
            aPhysics.AddCollisionEntry(prefSphere.Handle, collisionProperties);
            eTarget.Set(new engine.physics.components.Kinetic(
                prefSphere, collisionProperties));
            eTarget.Set(new engine.draw.components.OSDText(
                new Vector2(0, 30f),
                new Vector2(160f, 18f),
                $"polytope {worldFragment.NumericalId}",
                12,
                0x88226622,
                0x00000000,
                engine.draw.HAlign.Left) {MaxDistance = 800f});

        });
        worldFragment.Engine.QueueEntitySetupAction("nogame.furniture.polytopeBall", tSetupEntity);
    }
    

    public Func<Task> FragmentOperatorApply(engine.world.Fragment worldFragment) => new (async () =>
    {
        _rnd.Clear();
 
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

        int idx = (int)(_rnd.GetFloat() * nEstates);
        var polytopeEstate = potentialEstates[idx];
        _placePolytope(worldFragment, polytopeEstate);

    });

    public GeneratePolytopeOperator(
        engine.world.ClusterDesc clusterDesc,
        string strKey
    ) {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
        _rnd = new engine.RandomSource(_myKey);
    }
    
    
    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GeneratePolytopeOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}