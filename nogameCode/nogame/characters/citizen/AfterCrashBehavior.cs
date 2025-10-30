using System.Net.NetworkInformation;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave;
using engine.joyce;
using engine.joyce.components;
using engine.physics;
using static engine.Logger;

namespace nogame.characters.citizen;

public class AfterCrashBehavior : ABehavior
{
    private engine.Engine _engine;
    private DefaultEcs.Entity _entity;

    private IBehavior _oldBehavior = null;

    static private float LIFETIME = 10f;
    private float t = 0;
    private bool _deathAnimationTriggered = false;
    
    public required CharacterModelDescription CharacterModelDescription;
    

    public override void OnCollision(ContactEvent cev)
    {
        var me = cev.ContactInfo.PropertiesA;

        /*
         * I collided again with something, so increase my timer. 
         */
        t = builtin.tools.RandomSource.Instance.GetFloat()*2.0f + 0.5f;
    }
    

    public override void Sync(in DefaultEcs.Entity entity)
    {
    }
    
    
    public override void Behave(in Entity entity, float dt)
    {
        if (!_deathAnimationTriggered)
        {
            _deathAnimationTriggered = true;
            if (entity.Has<GPUAnimationState>() && entity.Has<engine.joyce.components.FromModel>())
            {
                ref var cGpuAnimationState = ref entity.Get<GPUAnimationState>();
                ref var cFromModel = ref entity.Get<engine.joyce.components.FromModel>();
                ref var model = ref cFromModel.Model;

                var mapAnimations = model.AnimationCollection.MapAnimations;
                if (mapAnimations != null && mapAnimations.Count > 0)
                {
                    string strAnimation = CharacterModelDescription.DeathAnimName;
                    if (!mapAnimations.ContainsKey(strAnimation))
                    {
                        int a = 1;
                    }
                
                    var animation = mapAnimations[strAnimation];
                    var animState = cGpuAnimationState.AnimationState;
                    if (animState != null)
                    {
                        animState.ModelAnimation = animation;
                        animState.ModelAnimationFrame = 0;
                        animState.Flags = (ushort)((uint)animState.Flags | (uint)AnimationState.IsOneShot);
                    } 
                }            
            }
        }
        if (t < LIFETIME)
        {
            t += dt;

            /*
             * Lift it up to the ground.
             * Warning: Shared with WASDPhysics
             */
            Vector3 vTotalImpulse = new Vector3(0f, -9.81f, 0f);

            float LevelUpThrust = 16f;
            float LevelDownThrust = 16f;

            lock (_engine.Simulation)
            {
                var prefTarget = entity.Get<engine.physics.components.Body>().Reference;
                
                Vector3 vTargetPos = prefTarget.Pose.Position;
                float heightAtTarget = I.Get<engine.world.MetaGen>().Loader.GetNavigationHeightAt(vTargetPos);
                #if false
                Vector3 vTargetVelocity = prefTarget.Velocity.Linear;
                {
                    var properDeltaY = 0;
                    var deltaY = vTargetPos.Y - (heightAtTarget + properDeltaY);
                    const float threshDiff = 0.01f;

                    Vector3 impulse;
                    float properVelocity = 0f;
                    if (deltaY < -threshDiff)
                    {
                        properVelocity = LevelUpThrust; // 1ms-1 up.
                    }
                    else if (deltaY > threshDiff)
                    {
                        properVelocity = -LevelDownThrust; // 1ms-1 down.
                    }

                    float deltaVelocity = properVelocity - vTargetVelocity.Y;
                    float fireRate = deltaVelocity;
                    impulse = new Vector3(0f, fireRate, 0f);
                    vTotalImpulse += impulse;
                }
                #endif


                /*
                 * Clip at bottom
                 */
                if (vTargetPos.Y < heightAtTarget)
                {
                    vTargetPos.Y = heightAtTarget;
                    prefTarget.Pose.Position = vTargetPos;
                }

                #if false
                float massPerson = CharacterCreator.PhysicsMass;
                entity.Set(new engine.joyce.components.Motion(prefTarget.Velocity.Linear));

                prefTarget.ApplyImpulse(vTotalImpulse * dt * massPerson, new Vector3(0f, 0f, 0f));
                #endif
                prefTarget.Awake = true;
            }
        }
        else
        {
            /*
             * Remove new behavior, doom entity.
             */
            ref engine.physics.components.Body cCitizenBody = ref entity.Get<engine.physics.components.Body>();
            cCitizenBody.PhysicsObject.RemoveContactListener();
            lock (_engine.Simulation)
            {
                var prefTarget = cCitizenBody.Reference;
                prefTarget.Awake = false;
                prefTarget.BecomeKinematic();
            }
            entity.Remove<engine.behave.components.Behavior>();
            _engine.AddDoomedEntity(entity);
        }
    }

    
    public AfterCrashBehavior(engine.Engine engine0, DefaultEcs.Entity entity0)
    {
        _engine = engine0;
        _entity = entity0;
        if (_entity.Has<engine.behave.components.Behavior>())
        {
            _oldBehavior = _entity.Get<engine.behave.components.Behavior>().Provider;
        }
    }
}