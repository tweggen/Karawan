
namespace engine
{
    /**
     * A scene is a way of presenting visual content.
     * Scenes can be added and removed to the engine.
     */
    public interface IScene : IModule
    {
        public void SceneOnLogicalFrame(float dt);


        public void SceneKickoff();
    }
}
