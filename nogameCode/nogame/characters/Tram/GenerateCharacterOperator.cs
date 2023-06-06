using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.world;
using engine.streets;
using static engine.Logger;   


namespace nogame.characters.tram;


class GenerateCharacterOperator : engine.world.IFragmentOperator
{
    private static object _classLock = new();
    private static engine.joyce.Material _jMaterialCube;

    private static engine.audio.Sound _jTramSound;

    private static engine.audio.Sound _getTramSound()
    {
        lock (_classLock)
        {
            if (_jTramSound == null)
            {
                _jTramSound = new engine.audio.Sound(
                    "tramnoise.ogg", true, 0.3f, 1.0f);
            }

            return _jTramSound;
        }
    }
    
    
    private static engine.joyce.InstanceDesc[] _jInstancesTram;
    private static engine.joyce.InstanceDesc _getTramMesh(int i)
    {
        lock(_classLock)
        {
            if( null==_jInstancesTram)
            {
                _jInstancesTram = new engine.joyce.InstanceDesc[1];
                _jInstancesTram[0] = builtin.loader.Obj.LoadModelInstance("tram1.obj");
                foreach (var ji in _jInstancesTram)
                {
                    /*
                     * Our models are 180 degree wrong.
                     */
                    ji.ModelTransform = Matrix4x4.CreateRotationY((float)Math.PI);
                }
            }
            return _jInstancesTram[i];
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
                _pbodySphere = new(0.5f);
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
        return $"7020/GenerateTramCharacterOperator/{_myKey}/";
    }
    
    public void FragmentOperatorApply(in engine.world.Fragment worldFragment)
    {
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
        int nCharacters = (int)((float)_clusterDesc.Size / 20f + 1f);
        if( nCharacters > _clusterDesc.GetNClosest() ) {
            nCharacters = _clusterDesc.GetNClosest();
        }
        Trace($"Generating {nCharacters} trams.");

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
                    int tramIdx = 0;
                    engine.joyce.InstanceDesc jInstanceDesc = _getTramMesh(tramIdx);

                    var wf = worldFragment;


                    var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
                    {
                        eTarget.Set(new engine.joyce.components.Instance3(jInstanceDesc));
                        eTarget.Set(new engine.behave.components.Behavior(
                            new Behavior(wf.Engine, _clusterDesc, chosenStreetPoint)
                                .SetSpeed(30f)
                                .SetHeight(10f)));

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
                        eTarget.Set(new engine.audio.components.MovingSound(_getTramSound(), 150f));
                        eTarget.Set(new engine.physics.components.Kinetic(prefSphere));
                    });

                    wf.Engine.QueueEntitySetupAction("nogame.characters.tram", tSetupEntity);


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
