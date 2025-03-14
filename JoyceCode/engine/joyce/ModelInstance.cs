namespace engine.joyce;


/**
 * Keep all per-instance information of a given instance of a model, e.g.
 * - Which animation is played back?
 * - Which animation frame is running
 */
public class ModelInstance
{
    /**
     * If we have been created from a model, this is the corresponding model.
     */
    public Model Model;

    public string AnimationName;
    public int AnimationFrameNumber;
}