using System;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.news;
using static engine.Logger;

namespace builtin.tunestreets;

public class Scene : IScene, IInputPart
{
    private object _lo = new();
    
    private Engine _engine;

    private static float MY_Z_ORDER = 20f;
    
    private engine.world.ClusterDesc _clusterDesc;

    private bool _shallGenerate = true;
    
    
    public void SceneOnLogicalFrame(float dt)
    {
    }


    private engine.world.ClusterDesc _createDefaultCluster()
    {
        builtin.tools.RandomSource rnd = new("clusters-mydear");
        var clusterDesc =  new engine.world.ClusterDesc("cluster-clusters-mydear-0");
        clusterDesc.Pos = new Vector3(-10f * rnd.GetFloat(), 0f, 10f);
        clusterDesc.Size = 1000f;
        clusterDesc.Name = engine.tools.NameGenerator.Instance().CreateWord(rnd);
        return clusterDesc;
    }


    public void InputPartOnInputEvent(Event ev)
    {
        if (ev.Type == Event.INPUT_KEY_PRESSED)
        {
            switch (ev.Code)
            {
                case "(escape)":
                    SceneDeactivate();
                    ev.IsHandled = true;
                    break;
                default:
                    break;
            }
        }
    }


    public void SceneDeactivate()
    {
        _shallGenerate = false;
        _engine.SceneSequencer.RemoveScene(this);
        I.Get<InputEventPipeline>().RemoveInputPart(this);
    }
    

    public void SceneActivate(Engine engine)
    {
        _engine = engine;

        I.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        _engine.SceneSequencer.AddScene(5, this);
        Task.Run(() =>
        {
            double aver = 0;
            int n = 0;
            while (true)
            {
                lock (_lo)
                {
                    if (!_shallGenerate)
                    {
                        if (n > 0)
                        {
                            Trace($"average duration {aver / n}ms.");
                        }
                        return;
                    }

                    DateTime nowStart = DateTime.Now;
                    _clusterDesc = _createDefaultCluster();
                    var strokeStore = _clusterDesc.StrokeStore();
                    DateTime nowEnd = DateTime.Now;
                    var duration = nowEnd - nowStart;
                    ++n;
                    aver += duration.TotalMilliseconds;
                }
            }
        });
    }

}