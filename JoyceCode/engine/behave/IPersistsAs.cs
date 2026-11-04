namespace engine.behave;


/**
 * A transient implementation - a behaviour or navigator that only exists for a moment,
 * such as a car's few seconds after a crash - that is saved as something else.
 *
 * InterfacePointerConverter writes PersistAs in its place: the object the transient one
 * would hand back to (null writes no implementation at all). So a game saved mid-crash
 * loads with the car driving, instead of trying to rebuild a state that only made sense
 * with the physics of the moment it was saved in.
 */
public interface IPersistsAs
{
    object PersistAs { get; }
}
