using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.world;
using engine.streets;
using static engine.Logger;   


namespace nogame.characters.car3;


class GenerateCharacterOperator : engine.world.IFragmentOperator
{
    private static object _classLock = new();

    private static engine.audio.Sound[] _jCar3Sound;

    private static engine.audio.Sound _getCar3Sound(int i)
    {
        lock (_classLock)
        {
            if (_jCar3Sound == null)
            {
                _jCar3Sound = new engine.audio.Sound[3];
                for (int j = 0; j < 3; ++j)
                {
                    _jCar3Sound[j] = new engine.audio.Sound(
                        "car3noisemono.ogg", true, 0.5f, 0.7f + j*0.3f);
                }
            }

            return _jCar3Sound[i];
        }
    }
    
    
    private static engine.joyce.InstanceDesc[] _jInstancesCar;
    private static engine.joyce.InstanceDesc _getCarMesh(int i)
    {
        lock(_classLock)
        {
            if( null==_jInstancesCar)
            {
                _jInstancesCar = new engine.joyce.InstanceDesc[3];
                _jInstancesCar[0] = builtin.loader.Obj.LoadModelInstance("car2.obj");
                _jInstancesCar[1] = builtin.loader.Obj.LoadModelInstance("car4.obj");
                _jInstancesCar[2] = builtin.loader.Obj.LoadModelInstance("car5.obj");
                foreach (var ji in _jInstancesCar)
                {
                    /*
                     * Our models are 180 degree wrong.
                     */
                    ji.ModelTransform = Matrix4x4.CreateRotationY((float)Math.PI);
                }
            }
            return _jInstancesCar[i];
        }
    }

    private static BepuPhysics.Collidables.TypedIndex _pshapeSphere;
    private static BepuPhysics.Collidables.Sphere _pbodySphere;
    private static BepuPhysics.Collidables.TypedIndex _getSphereShape(in Engine engine)
    {
        lock(_classLock)
        {
            if( !_pshapeSphere.Exists )
            {
                _pbodySphere = new(1f);
                lock (engine.Simulation)
                {
                    _pshapeSphere = engine.Simulation.Shapes.Add(_pbodySphere);
                }
            }
            return _pshapeSphere;
        }
    }


    private ClusterDesc _clusterDesc;
    private engine.RandomSource _rnd;
    private string _myKey;

    private bool _trace = false;

    private int _characterIndex = 0;

    public string FragmentOperatorGetPath()
    {
        return $"7020/GenerateCar3CharacterOperatar/{_myKey}/";
    }
    
    public void FragmentOperatorApply(in engine.world.Fragment worldFragment)
    {
        var aPhysics = worldFragment.Engine.GetAPhysics();
        
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

        if (_trace) Trace( $"cluster '{_clusterDesc.Id}' ({_clusterDesc.Pos.X}, {_clusterDesc.Pos.Z}) in range");
        _rnd.clear();

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
        int nCharacters = (int)((float)l * 7f / 10f);

        for (int i=0; i<nCharacters; i++)
        {

            var idxPoint = (int)(_rnd.getFloat() * l);
            var idx = 0;
            StreetPoint chosenStreetPoint = null;
            foreach (var sp in streetPoints )
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
                if (_trace) Trace($"Starting on streetpoint $idxPoint ${chosenStreetPoint.Pos.X}, ${chosenStreetPoint.Pos.Y}.");

                ++_characterIndex;
                {
                    int carIdx = (int)(_rnd.getFloat() * 3f);
                    engine.joyce.InstanceDesc jInstanceDesc = _getCarMesh(carIdx);

                    var wf = worldFragment;

                    int fragmentId = worldFragment.NumericalId;
                    
                    var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
                    {          
                        eTarget.Set(new engine.world.components.FragmentId(fragmentId));
                        eTarget.Set(new engine.joyce.components.Instance3(jInstanceDesc));

                        eTarget.Set(new engine.behave.components.Behavior(
                            new Behavior(wf.Engine, _clusterDesc, chosenStreetPoint)
                                .SetSpeed((40f+_rnd.getFloat()*30f+(float)carIdx * 20f)/3.6f)));

                        eTarget.Set(new engine.audio.components.MovingSound(
                            _getCar3Sound(carIdx), 150f));


                        BodyHandle phandleSphere = wf.Engine.Simulation.Bodies.Add(
                            BodyDescription.CreateKinematic(
                                new Vector3(0f, 0f, 0f), // infinite mass, this is a kinematic object.
                                new BepuPhysics.Collidables.CollidableDescription(
                                    _getSphereShape(wf.Engine),
                                    0.1f),
                                new BodyActivityDescription(0.01f)
                            )
                        );
                        BodyReference prefSphere = wf.Engine.Simulation.Bodies.GetBodyReference(phandleSphere);
                        engine.physics.CollisionProperties collisionProperties = 
                            new engine.physics.CollisionProperties
                                { Name = "nogame.characters.car3", IsTangible = true };
                        aPhysics.AddCollisionEntry(prefSphere.Handle, collisionProperties);
                        eTarget.Set(new engine.physics.components.Kinetic( 
                            prefSphere, collisionProperties ));
                    });

                    wf.Engine.QueueEntitySetupAction("nogame.characters.car3", tSetupEntity);


                }
            }
            else
            {
                if (_trace) Trace("No streetpoint found.");
            }
        }
    }


    public GenerateCharacterOperator(
        in ClusterDesc clusterDesc, in string strKey)
    {
        _clusterDesc = clusterDesc;
        _myKey = strKey;
        _rnd = new engine.RandomSource(strKey);
    }
}
