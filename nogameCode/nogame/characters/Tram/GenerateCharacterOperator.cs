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
    private class Context
    {
        public builtin.tools.RandomSource Rnd;
        public engine.world.Fragment Fragment;
    }
    
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
        
        var ctx = new Context()
        {
            Rnd = new(_myKey),
            Fragment = worldFragment
        };

        if (_trace) Trace($"cluster '{_clusterDesc.IdString}' ({_clusterDesc.Pos.X}, {_clusterDesc.Pos.Z}) in range");
        ctx.Rnd.Clear();


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

        var listInFragment = _clusterDesc.GetStreetPointsInFragment(worldFragment.IdxFragment);
        var nStreetPoints = listInFragment.Count;
        int nCharacters = nStreetPoints / 14;
        if (nCharacters > 0)
        {
            ModelCacheParams mcp = new()
            {
                Url = "tram1.obj",
                Params = new()
                {
                    GeomFlags = 0
                                | InstantiateModelParams.CENTER_X
                                | InstantiateModelParams.CENTER_Z
                                | InstantiateModelParams.ROTATE_Y180
                                | InstantiateModelParams.REQUIRE_ROOT_INSTANCEDESC,
                    MaxDistance = propMaxDistance,
                }
            };
            Model model = await I.Get<ModelCache>().LoadModel(mcp);

            for (int i = 0; i < nCharacters; i++)
            {
                var idxPoint = ctx.Rnd.GetInt(nStreetPoints - 1);
                var chosenStreetPoint = listInFragment[idxPoint];

                if (_trace)
                {
                    Trace($"Starting on streetpoint $idxPoint ${chosenStreetPoint.Pos.X}, ${chosenStreetPoint.Pos.Y}.");
                }

                ++_characterIndex;
                {
                    var wf = worldFragment;

                    int fragmentId = worldFragment.NumericalId;

                    var tSetupEntity = new Action<DefaultEcs.Entity>((DefaultEcs.Entity eTarget) =>
                    {
                        eTarget.Set(new engine.world.components.Owner(fragmentId));
                        
                        eTarget.Set(new engine.behave.components.Behavior(
                                new Behavior(wf.Engine, _clusterDesc, chosenStreetPoint)
                                    .SetSpeed(30f)
                                    .SetHeight(10f))
                            { MaxDistance = (short)propMaxDistance }
                        );
                        
                        eTarget.Set(new engine.audio.components.MovingSound(GetTramSound(), 150f));
                        
                        /*
                         * We need to set a preliminary Transform3World component. Invisible, but inside the fragment.
                         * That way, the character will not be cleaned up immediately.
                         */
                        eTarget.Set(new engine.joyce.components.Transform3ToWorld(0, 0,
                            Matrix4x4.CreateTranslation(worldFragment.Position)));

                        I.Get<ModelCache>().BuildPerInstance(eTarget, model, mcp);
                    });

                    wf.Engine.QueueEntitySetupAction("nogame.characters.tram", tSetupEntity);


                }
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
