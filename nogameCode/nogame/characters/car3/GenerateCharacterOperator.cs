using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.joyce;
using engine.physics;
using engine.world;
using engine.streets;
using static engine.Logger;   


namespace nogame.characters.car3;


class GenerateCharacterOperator : engine.world.IFragmentOperator
{
    private class Context
    {
        public builtin.tools.RandomSource Rnd;
        public engine.world.Fragment Fragment;
    }
    
    static public readonly string PhysicsName = "nogame.characters.car3";
    static private readonly float PhysicsMass = 500f;
    static private readonly float PhysicsRadius = 5f;
    static public BodyInertia PInertiaSphere = 
        new BepuPhysics.Collidables.Sphere(
            GenerateCharacterOperator.PhysicsRadius)
        .ComputeInertia(GenerateCharacterOperator.PhysicsMass);

    private static object _classLock = new();

    private static engine.audio.Sound[] _jCar3Sound;
    private static ShapeFactory _shapeFactory = I.Get<ShapeFactory>();

    private static List<string> _primarycolors = new List<string>()
    {
#if false
        "#ff88ee33",
        "#ff224411",
        "#ff444433",
        "#ffcc2244",
        "#ff164734",
        "#ff209897"
#else
        "#ff000000",
        "#ff0000ff",
        "#ff00ff00",
        // "#ff00ffff",
        "#ffff0000",
        "#ffff00ff",
        "#ffffff00"
        //"#ffffffff"
#endif
    };

    private static engine.audio.Sound _getCar3Sound(int i)
    {
        lock (_classLock)
        {
            if (_jCar3Sound == null)
            {
                _jCar3Sound = new engine.audio.Sound[4];
                for (int j = 0; j < 4; ++j)
                {
                    _jCar3Sound[j] = new engine.audio.Sound(
                        "car3noisemono.ogg", true, 0.5f, 0.7f + j*0.3f);
                }
            }

            return _jCar3Sound[i];
        }
    }
    
    
    private ClusterDesc _clusterDesc;
    private string _myKey;

    private static bool _trace = false;

    private int _characterIndex = 0;

    public string FragmentOperatorGetPath()
    {
        return $"7020/GenerateCar3CharacterOperatar/{_myKey}/{_clusterDesc.IdString}";
    }
    
    
    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    private static string _carFileName(int carIdx)
    {
        return $"car{5 + carIdx}.obj";
    }



    static public StreetPoint? ChooseStreetPoint(builtin.tools.RandomSource rnd, Fragment worldFragment, ClusterDesc clusterDesc)
    {
        /*
         * Load all prerequisites
         */
        StreetPoint chosenStreetPoint = null;

        /*
         * First, generate the set of street points within this fragemnt.
         */
        var listInFragment = clusterDesc.GetStreetPointsInFragment(worldFragment.IdxFragment);
        var nStreetPoints = listInFragment.Count(); 
        if (nStreetPoints == 0)
        {
            return null;
        }

        var idxPoint = rnd.GetInt(nStreetPoints - 1);
        return listInFragment[idxPoint];
    }

    
    public static async void GenerateCharacter(ClusterDesc clusterDesc, Fragment worldFragment)
    {
        
    }


    public static async Task<DefaultEcs.Entity> GenerateCharacter(
        builtin.tools.RandomSource rnd, ClusterDesc clusterDesc, Fragment worldFragment, StreetPoint chosenStreetPoint, int seed=0)
    {
        TaskCompletionSource<DefaultEcs.Entity> taskCompletionSource = new();
        Task<DefaultEcs.Entity> taskResult = taskCompletionSource.Task;
        
        float propMaxDistance = (float)engine.Props.Get("nogame.characters.car3.maxDistance", 800f);

        {
            int carIdx = (int)(rnd.GetFloat() * 4f);
            int colorIdx = (int)(rnd.GetFloat() * (float)_primarycolors.Count);

            builtin.loader.ModelProperties props = new()
            {
                ["primarycolor"] = _primarycolors[colorIdx],
            };
            InstantiateModelParams instantiateModelParams = new()
            {
                GeomFlags = 0 | InstantiateModelParams.CENTER_X
                              | InstantiateModelParams.CENTER_Z
                              | InstantiateModelParams.ROTATE_Y180
                              | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC,
                MaxDistance = propMaxDistance
            };

            Model model = await I.Get<ModelCache>().Instantiate(
                _carFileName(carIdx), props, instantiateModelParams);
            InstanceDesc jInstanceDesc = model.RootNode.InstanceDesc;

            var wf = worldFragment;

            int fragmentId = worldFragment.NumericalId;

            /*
             * Now, first, from any of the worker threads, prepare the physics.
             * There's no contact listeners etc added and they're off, so no worries.
             */
            engine.physics.Object po;
            BodyReference prefSphere;
            lock (wf.Engine.Simulation)
            {
                var shape = _shapeFactory.GetSphereShape(jInstanceDesc.AABBTransformed.Radius);
                po = new engine.physics.Object(wf.Engine, default, Vector3.Zero, Quaternion.Identity, shape);
                prefSphere = wf.Engine.Simulation.Bodies.GetBodyReference(new BodyHandle(po.IntHandle));
                prefSphere.Awake = false;
            }

            /*
             * Now, using the prepared physics, add the actual entity components.
             */
            var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
            {
#if DEBUG
                Stopwatch sw = new();
                sw.Start();
#endif

                eTarget.Set(new engine.world.components.FragmentId(fragmentId));

#if DEBUG
                float millisAfterFragmentId = (float)sw.Elapsed.TotalMilliseconds;
#endif

                {
                    builtin.tools.ModelBuilder modelBuilder = new(worldFragment.Engine, model, instantiateModelParams);
                    modelBuilder.BuildEntity(eTarget);
                }
#if DEBUG
                float millisAfterBuild = (float)sw.Elapsed.TotalMilliseconds;
#endif

                eTarget.Set(new engine.behave.components.Behavior(
                    new car3.Behavior(wf.Engine, clusterDesc, chosenStreetPoint, seed)
                    {
                        Speed = (30f + rnd.GetFloat() * 20f + (float)carIdx * 20f) / 3.6f
                    })
                {
                    MaxDistance = (short)propMaxDistance
                });

#if DEBUG
                float millisAfterBehavior = (float)sw.Elapsed.TotalMilliseconds;
#endif

                eTarget.Set(new engine.audio.components.MovingSound(
                    _getCar3Sound(carIdx), 150f));

#if DEBUG
                float millisAfterMovingSound = (float)sw.Elapsed.TotalMilliseconds;
#endif


                engine.physics.CollisionProperties collisionProperties =
                    new engine.physics.CollisionProperties
                    {
                        Entity = eTarget,
                        Flags =
                            CollisionProperties.CollisionFlags.IsTangible
                            | CollisionProperties.CollisionFlags.IsDetectable
                            | CollisionProperties.CollisionFlags.TriggersCallbacks,
                        Name = PhysicsName,
                        LayerMask = 0x0002,
                    };
                po.CollisionProperties = collisionProperties;
                po.Entity = eTarget;
                eTarget.Set(new engine.physics.components.Body(po, prefSphere));
                
                /*
                 * We need to set a preliminary Transform3World asset. Invisible, but inside the fragment.
                 */
                eTarget.Set(new engine.joyce.components.Transform3ToWorld(0, 0, Matrix4x4.CreateTranslation(worldFragment.Position)));

#if DEBUG
                float millisAfterBody = (float)sw.Elapsed.TotalMilliseconds;
                sw.Stop();
                if (sw.Elapsed.TotalMilliseconds > 3f)
                {
                    int a;
                }
#endif
                taskCompletionSource.SetResult(eTarget);

            });
            wf.Engine.QueueEntitySetupAction("nogame.characters.car3", tSetupEntity);

        }
        return taskResult.Result;
    }



    public Func<Task> FragmentOperatorApply(engine.world.Fragment worldFragment, FragmentVisibility visib) => new (async () =>
    {
        if (0 == (visib.How & engine.world.FragmentVisibility.Visible3dAny))
        {
            return;
        }
        
        float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
        float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

        /*
         * We don't apply the operator if the fragment completely is
         * outside our boundary box (the cluster)
         */
        {
            {
                float csh = _clusterDesc.Size / 2.0f;
                float fsh = engine.world.MetaGen.FragmentSize / 2.0f;
                if (
                    (cx - csh) > (fsh)
                    || (cx + csh) < (-fsh)
                    || (cz - csh) > (fsh)
                    || (cz + csh) < (-fsh)
                )
                {
                    // trace( "Too far away: x="+_clusterDesc.x+", z="+_clusterDesc.z);
                    return;
                }
            }
        }

        var ctx = new Context()
        {
            Rnd = new(_myKey),
            Fragment = worldFragment
        };

        if (_trace) Trace($"cluster '{_clusterDesc.IdString}' ({_clusterDesc.Pos.X}, {_clusterDesc.Pos.Z}) in range");
        
        var strokeStore = _clusterDesc.StrokeStore();
        IList<StreetPoint> streetPoints = strokeStore.GetStreetPoints();
        if (streetPoints.Count == 0)
        {
            return;
        }
        int l = streetPoints.Count;
        
        /*
         * Now, that we have read the cluster description that is associated, we
         * can place the characters randomly on the streetpoints.
         *
         * TXWTODO: We would be more intersint to place them on the streets.
         */

        // TXWTODO: The target number of characters shall be a property of the spawnoperator.
        int nCharacters = (int)((float)l * 8f / 10f);

        for (int i = 0; i < nCharacters; i++)
        {   
            StreetPoint? chosenStreetPoint = ChooseStreetPoint(ctx.Rnd, worldFragment, _clusterDesc);

            if (null != chosenStreetPoint)
            {
                if (_trace) Trace($"Starting on streetpoint $idxPoint ${chosenStreetPoint.Pos.X}, ${chosenStreetPoint.Pos.Y}.");

                ++_characterIndex;
                GenerateCharacter(ctx.Rnd, _clusterDesc, worldFragment, chosenStreetPoint);
            }
            else
            {
                if (_trace) Trace("No streetpoint found.");
            }
        }
    });

    
    public GenerateCharacterOperator(
        in ClusterDesc clusterDesc, in string strKey)
    {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
    }
    
    
    public static IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GenerateCharacterOperator(
            (ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}
