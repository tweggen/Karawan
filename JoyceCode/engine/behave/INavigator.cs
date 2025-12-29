using System.Numerics;

namespace engine.behave;

public interface INavigator
{
    public void NavigatorGetTransformation(out Vector3 position, out Quaternion orientation);
    public void NavigatorSetTransformation(Vector3 vPos3, Quaternion qOrientation);
    public void NavigatorBehave(float dt);

    public void NavigatorLoad();
    
    public float Speed { get; set; }
}