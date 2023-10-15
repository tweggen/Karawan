using DefaultEcs.Resource;
using System;
using System.Linq;
using engine;
using static engine.Logger;

namespace Splash.systems;

/**
 * Create platform rendering infos for this entity.
 *
 * This creates a PfInstance component reflecting the instance3 component.
 * Most prominently, it uses the same order and association of materials and meshes.
 */
[DefaultEcs.System.With(typeof(engine.joyce.components.Camera3))]
[DefaultEcs.System.Without(typeof(Splash.components.PfRenderbuffer))]
public class CreatePfRenderbufferSystem : DefaultEcs.System.AEntitySetSystem<engine.Engine>
{
    private engine.Engine _engine;

    private int _runNumber = 0;

    protected override void PreUpdate(engine.Engine state)
    {
        ++_runNumber;
    }

    protected override void PostUpdate(engine.Engine state)
    {
    }

    protected override void Update(engine.Engine state, ReadOnlySpan<DefaultEcs.Entity> entities)
    {
        foreach (var entity in entities)
        {
            var cCamera3 = entity.Get<engine.joyce.components.Camera3>();
            engine.joyce.Renderbuffer jRenderbuffer = cCamera3.Renderbuffer;

            if (null != jRenderbuffer)
            {
                /*
                 * Create the platform entity. It will be filled by the instance manager.
                 */
                entity.Set(new components.PfRenderbuffer(jRenderbuffer));
            }
        }
    }

    public unsafe CreatePfRenderbufferSystem()
        : base(I.Get<Engine>().GetEcsWorld())
    {
        _engine = I.Get<Engine>();
    }
}
