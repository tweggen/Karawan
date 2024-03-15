using BepuPhysics;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.joyce;
using engine.world;
using engine.streets;
using static engine.Logger;   


namespace nogame.characters.tram;


class GenerateCharacterOperator : engine.world.IFragmentOperator
{
    private static object _classLock = new();
    private static engine.joyce.Material _jMaterialCube;

    private static engine.audio.Sound _jTramSound;

    public static engine.audio.Sound GetTramSound()
    {
        lock (_classLock)
        {
            if (_jTramSound == null)
            {
                _jTramSound = new engine.audio.Sound(
                    "tramnoise.ogg", true, 1.2f, 1.0f);
            }

            return _jTramSound;
        }
    }
    
 
    private ClusterDesc _clusterDesc;
    private builtin.tools.RandomSource _rnd;
    private string _myKey;

    private bool _trace = false;

    private int _characterIndex = 0;

    public string FragmentOperatorGetPath()
    {
        return $"7020/GenerateTramCharacterOperator/{_myKey}/{_clusterDesc.IdString}";
    }
    
    
    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
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

        if (_trace) Trace($"cluster '{_clusterDesc.IdString}' ({_clusterDesc.Pos.X}, {_clusterDesc.Pos.Z}) in range");
        _rnd.Clear();


        float propMaxDistance = (float) engine.Props.Get("nogame.characters.tram.maxDistance", 1600f); 
        
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
        if (nCharacters > _clusterDesc.GetNClosest())
        {
            nCharacters = _clusterDesc.GetNClosest();
        }
        // Trace($"Generating {nCharacters} trams.");

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
                    Model model = await ModelCache.Instance().Instantiate(
                        "tram1.obj", null, new InstantiateModelParams()
                        {
                            GeomFlags = 0
                                        | InstantiateModelParams.CENTER_X
                                        | InstantiateModelParams.CENTER_Z
                                        | InstantiateModelParams.ROTATE_Y180
                                        | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC,
                            MaxDistance = propMaxDistance,
                        });
                    engine.joyce.InstanceDesc jInstanceDesc = model.RootNode.InstanceDesc;
 
                    var wf = worldFragment;

                    int fragmentId = worldFragment.NumericalId;

                    var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
                    {
                        eTarget.Set(new engine.world.components.FragmentId(fragmentId));
                        eTarget.Set(new engine.joyce.components.Instance3(jInstanceDesc));
                        eTarget.Set(new engine.behave.components.Behavior(
                            new Behavior(wf.Engine, _clusterDesc, chosenStreetPoint)
                                .SetSpeed(30f)
                                .SetHeight(10f))
                            { MaxDistance = propMaxDistance }
                        );
                        eTarget.Set(new engine.audio.components.MovingSound(GetTramSound(), 150f));
                    });

                    wf.Engine.QueueEntitySetupAction("nogame.characters.tram", tSetupEntity);


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
