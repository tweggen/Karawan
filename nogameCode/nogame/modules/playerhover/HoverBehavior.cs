using DefaultEcs;
using engine;
using engine.behave;
using engine.news;
using engine.physics;

namespace nogame.modules.playerhover;


/**
 * Player behavior when driving a car.
 *
 * - registers collisions with cubes, polytopes and other cars.
 * - creates the underlying wasd controller that actually controls the car.
 */
public class HoverBehavior : ABehavior
{
    public static string PLAYER_COLLISION_ANONYMOUS = "nogame.playerhover.collision.anonymous";
    public static string PLAYER_COLLISION_CUBE = "nogame.playerhover.collision.cube";
    public static string PLAYER_COLLISION_CAR3 = "nogame.playerhover.collision.car3";
    public static string PLAYER_COLLISION_CITIZEN = "nogame.playerhover.collision.citizen";
    public static string PLAYER_COLLISION_POLYTOPE = "nogame.playerhover.collision.polytope";
    
    private HoverController _controllerHoverController = null;
    private DefaultEcs.Entity _eTarget;
    private bool _cutCollisions = (bool) engine.Props.Get("nogame.CutCollision", false);

    public required float MassTarget { get; init; }
    
    public override void OnCollision(ContactEvent cev)
    {
        if (_cutCollisions) return;
        
        /*
         * If this contact involved us, we store the other contact info in this variable.
         * If the other does not have collision properties, this variable also is empty.
         */
        CollisionProperties other = cev.ContactInfo.PropertiesB;
        
        if (other == null)
        {
            cev.Type = PLAYER_COLLISION_ANONYMOUS;
            I.Get<EventQueue>().Push(cev);
            return;
        }

        /*
         * Now let's check for explicit other components.
         */
        if (other.Name == nogame.characters.cubes.GenerateCharacterOperator.PhysicsName)
        {
            cev.Type = PLAYER_COLLISION_CUBE;
            I.Get<EventQueue>().Push(cev);
        }
        else if (other.Name == "nogame.furniture.polytopeBall")
        {
            cev.Type = PLAYER_COLLISION_POLYTOPE;
            I.Get<EventQueue>().Push(cev);
        } 
        else if (other.Name == "nogame.characters.car3")
        {
            cev.Type = PLAYER_COLLISION_CAR3;
            I.Get<EventQueue>().Push(cev);
        } 
        else if (other.Name == "nogame.characters.citizen")
        {
            cev.Type = PLAYER_COLLISION_CITIZEN;
            I.Get<EventQueue>().Push(cev);
        }
    }


    public override void OnDetach(in Entity entity)
    {
        _controllerHoverController.ModuleDeactivate();
        _controllerHoverController.Dispose();
        _controllerHoverController = null;
        _engine = null;
    }
    
    
    public override void OnAttach(in engine.Engine engine0, in Entity entity)
    {
        _engine = engine0;
        _eTarget = entity;
        
        /*
         * And the ship's controller
         */

        _controllerHoverController = new HoverController()
        {
            Target = _eTarget,
            MassTarget = MassTarget
        };
        _controllerHoverController.ModuleActivate();
    }
}