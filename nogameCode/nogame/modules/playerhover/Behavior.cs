using DefaultEcs;
using engine;
using engine.news;
using engine.physics;

namespace nogame.modules.playerhover;

public class Behavior : engine.IBehavior
{
    public static string PLAYER_COLLISION_ANONYMOUS = "nogame.playerhover.collision.anonymous";
    public static string PLAYER_COLLISION_CUBE = "nogame.playerhover.collision.cube";
    public static string PLAYER_COLLISION_CAR3 = "nogame.playerhover.collision.car3";
    public static string PLAYER_COLLISION_POLYTOPE = "nogame.playerhover.collision.polytope";
    
    private WASDPhysics _controllerWASDPhysics = null;
    private engine.Engine _engine = null;
    private DefaultEcs.Entity _eShip;
    
    public void OnCollision(ContactEvent cev)
    {
        /*
         * If this contact involved us, we store the other contact info in this variable.
         * If the other does not have collision properties, this variable also is empty.
         */
        CollisionProperties other = cev.ContactInfo.PropertiesB;
        
        if (other == null)
        {
            cev.Type = PLAYER_COLLISION_ANONYMOUS;
            Implementations.Get<EventQueue>().Push(cev);
            return;
        }

        /*
         * Now let's check for explicit other components.
         */
        if (other.Name == nogame.characters.cubes.GenerateCharacterOperator.PhysicsName)
        {
            cev.Type = PLAYER_COLLISION_CUBE;
            Implementations.Get<EventQueue>().Push(cev);
        }
        else if (other.Name == "nogame.furniture.polytopeBall")
        {
            cev.Type = PLAYER_COLLISION_POLYTOPE;
            Implementations.Get<EventQueue>().Push(cev);
        } 
        else if (other.Name == "nogame.characters.car3")
        {
            cev.Type = PLAYER_COLLISION_CAR3;
            // Implementations.Get<EventQueue>().Push(cev);
        }
    }

    public void Sync(in Entity entity)
    {
        /*
         * We should update the behavior from reality, in case it has a state.
         * However, we are stateless. 
         */
    }
    

    public void Behave(in Entity entity, float dt)
    {
    }

    
    public void OnDetach(in Entity entity)
    {
        _controllerWASDPhysics.ModuleDeactivate();
        _controllerWASDPhysics.Dispose();
        _controllerWASDPhysics = null;
        _engine = null;
    }
    
    
    public void OnAttach(in engine.Engine engine0, in Entity entity)
    {
        _engine = engine0;
        _eShip = entity;
        
        /*
         * And the ship's controller
         */
        
        _controllerWASDPhysics = new WASDPhysics(_eShip, playerhover.Module.MassShip);
        _controllerWASDPhysics.ModuleActivate(_engine);
    }
}