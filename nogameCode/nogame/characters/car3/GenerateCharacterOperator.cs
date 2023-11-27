using BepuPhysics;
using System;
using System.Collections.Generic;
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

    static public readonly string PhysicsName = "nogame.characters.car3";
    static public readonly float PhysicsMass = 500f;
    static public readonly float PhysicsRadius = 5f;
    
    private static object _classLock = new();

    private static engine.audio.Sound[] _jCar3Sound;

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
    
    
    private static SortedDictionary<float, BepuPhysics.Collidables.TypedIndex> _mapPshapeSphere = new();
    private static SortedDictionary<float, BepuPhysics.Collidables.Sphere> _mapPbodySphere = new();
    public static BepuPhysics.Collidables.TypedIndex GetSphereShape(float radius, in Engine engine)
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


    private ClusterDesc _clusterDesc;
    private builtin.tools.RandomSource _rnd;
    private string _myKey;

    private bool _trace = false;

    private int _characterIndex = 0;

    public string FragmentOperatorGetPath()
    {
        return $"7020/GenerateCar3CharacterOperatar/{_myKey}/{_clusterDesc.Id}";
    }
    
    
    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    private string _carFileName(int carIdx)
    {
        return $"car{5 + carIdx}.obj";
    }


    public Func<Task> FragmentOperatorApply(engine.world.Fragment worldFragment) => new (async () =>
    {
        var aPhysics = I.Get<engine.physics.API>();

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

        float propMaxDistance = (float) engine.Props.Get("nogame.characters.car3.maxDistance", 800f); 
        
        if (_trace) Trace($"cluster '{_clusterDesc.Id}' ({_clusterDesc.Pos.X}, {_clusterDesc.Pos.Z}) in range");
        _rnd.Clear();

        /*
         * Now, that we have read the cluster description that is associated, we
         * can place the characters randomly on the streetpoints.
         *
         * TXWTODO: We would be more intersint to place them on the streets.
         */

        var strokeStore = _clusterDesc.StrokeStore();
        IList<StreetPoint> streetPoints = strokeStore.GetStreetPoints();
        if (streetPoints.Count == 0)
        {
            return;
        }

        int l = streetPoints.Count;
        int nCharacters = (int)((float)l * 8f / 10f);

        for (int i = 0; i < nCharacters; i++)
        {

            var idxPoint = (int)(_rnd.GetFloat() * l);
            var idx = 0;
            StreetPoint chosenStreetPoint = null;
            foreach (var sp in streetPoints)
            {
                if (idx == idxPoint)
                {
                    chosenStreetPoint = sp;
                    break;
                }

                idx++;
            }

            if (!chosenStreetPoint.HasStrokes())
            {
                continue;
            }

            /*
             * Check, wether the given street point really is inside our fragment.
             * That way, every fragment owns only the characters spawn on their
             * territory.
             */
            {
                float px = chosenStreetPoint.Pos.X + _clusterDesc.Pos.X;
                float pz = chosenStreetPoint.Pos.Y + _clusterDesc.Pos.Z;
                if (!worldFragment.IsInside(new Vector2(px, pz)))
                {
                    chosenStreetPoint = null;
                }
            }
            if (null != chosenStreetPoint)
            {
                if (_trace)
                    Trace($"Starting on streetpoint $idxPoint ${chosenStreetPoint.Pos.X}, ${chosenStreetPoint.Pos.Y}.");

                ++_characterIndex;
                {
                    int carIdx = (int)(_rnd.GetFloat() * 4f);
                    int colorIdx = (int)(_rnd.GetFloat() * (float)_primarycolors.Count);
                    
                    builtin.loader.ModelProperties props = new()
                    {
                        ["primarycolor"] = _primarycolors[colorIdx],
                    };
                    Model model = await ModelCache.Instance().Instantiate(
                        _carFileName(carIdx), props, new InstantiateModelParams()
                    {
                        GeomFlags = 0
                                    | InstantiateModelParams.CENTER_X
                                    | InstantiateModelParams.CENTER_Z
                                    | InstantiateModelParams.ROTATE_Y180,
                        MaxDistance = propMaxDistance
                    });
                    InstanceDesc jInstanceDesc = model.RootNode.InstanceDesc;
                    ModelInfo modelInfo = model.ModelInfo;
                    
                    var wf = worldFragment;

                    int fragmentId = worldFragment.NumericalId;

                    var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
                    {
                        eTarget.Set(new engine.world.components.FragmentId(fragmentId));
                        eTarget.Set(new engine.joyce.components.Instance3(jInstanceDesc));

                        eTarget.Set(new engine.behave.components.Behavior(
                            new car3.Behavior(wf.Engine, _clusterDesc, chosenStreetPoint)
                            { 
                                Speed = (30f + _rnd.GetFloat() * 20f + (float)carIdx * 20f) / 3.6f
                            })
                        {
                            MaxDistance = propMaxDistance
                        });

                        eTarget.Set(new engine.audio.components.MovingSound(
                            _getCar3Sound(carIdx), 150f));


                        BodyHandle phandleSphere = wf.Engine.Simulation.Bodies.Add(
                            BodyDescription.CreateKinematic(
                                new Vector3(0f, 0f, 0f), // infinite mass, this is a kinematic object.
                                new BepuPhysics.Collidables.CollidableDescription(
                                    GetSphereShape(modelInfo.AABB.Radius, wf.Engine),
                                    0.1f),
                                new BodyActivityDescription(0.01f)
                            )
                        );
                        BodyReference prefSphere = wf.Engine.Simulation.Bodies.GetBodyReference(phandleSphere);
                        engine.physics.CollisionProperties collisionProperties =
                            new engine.physics.CollisionProperties
                            {
                                Entity = eTarget,
                                Flags = 
                                    CollisionProperties.CollisionFlags.IsTangible 
                                    | CollisionProperties.CollisionFlags.IsDetectable
                                    | CollisionProperties.CollisionFlags.TriggersCallbacks,
                                Name = PhysicsName,
                            };
                        aPhysics.AddCollisionEntry(prefSphere.Handle, collisionProperties);
                        eTarget.Set(new engine.physics.components.Kinetic(
                            prefSphere, collisionProperties));
                    });

                    wf.Engine.QueueEntitySetupAction("nogame.characters.car3", tSetupEntity);


                }
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
        _rnd = new builtin.tools.RandomSource(strKey);
    }
    
    
    public static IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GenerateCharacterOperator(
            (ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}
