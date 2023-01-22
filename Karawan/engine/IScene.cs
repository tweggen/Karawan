
namespace Karawan.engine
{
    /**
     * A scene is a way of presenting visual content.
     * Scenes can be added and removed to the engine.
     */
    public interface IScene
    {
        public void SceneOnLogicalFrame(float dt);


        /**
         * Trigger removing this scene from the engine
         */
        public void SceneDeactivate();

        /**
         * Trigger adding this scene to the engine.
         */
        public void SceneActivate(engine.Engine engine);
    }
}
