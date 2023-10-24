using System;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using builtin.tools;
using engine;
using engine.joyce.components;
using nogame.characters.cubes;
using Behavior = engine.behave.components.Behavior;

namespace nogame.modules.demo;

public class Flyalong : AModule
{
    private Engine _engine;

    private builtin.controllers.FollowCameraController _ctrlFollowCamera;

    private Timer _selectNewTimer;
    private RandomSource _rnd;


    private void _detachSubject()
    {
        if (null != _ctrlFollowCamera)
        {
            _ctrlFollowCamera.DeactivateController();
            _ctrlFollowCamera = null;
        }
    }


    private void _attachNewSubject(object? sender, ElapsedEventArgs e)
    {
        _engine.QueueMainThreadAction(() =>
        {
            _detachSubject();
            var eCam = _engine.GetCameraEntity();
            if (!eCam.IsAlive || !eCam.Has<Camera3>())
            {
                Task.Delay(50).ContinueWith(t => _attachNewSubject(sender, e));
                return;
            }

            var behaving = _engine.GetEcsWorld().GetEntities().With<engine.behave.components.Behavior>().AsEnumerable();
            int nBehaving = behaving.Count();
            DefaultEcs.Entity eCarrot = default;
            do
            {
                int idx = (int)(_rnd.GetFloat() * nBehaving);
                int i = 0;
                foreach (var e in behaving)
                {
                    if (i == idx)
                    {
                        eCarrot = e;
                        break;
                    }

                    ++i;
                }
            } while (!eCarrot.IsAlive);

            /*
             * Create a camera controller that follows some subject.
             */
            _ctrlFollowCamera = new(_engine, eCam, eCarrot);
            _ctrlFollowCamera.ActivateController();
            
        });
    }
    
    
    public void ModuleDeactivate()
    {
        _detachSubject();
        _engine.RemoveModule(this);
    }
    
    
    public void ModuleActivate(Engine engine)
    {
        _engine = engine;
        _engine.AddModule(this);
        _selectNewTimer = new(10000);
        _selectNewTimer.Elapsed += _attachNewSubject;
        _rnd = new builtin.tools.RandomSource("demo");
    }
}