using System.Numerics;

namespace engine.behave;

public interface INavigator
{
    void NavigatorGetTransformation(out Vector3 position, out Quaternion orientation);
    void NavigatorSetTransformation(Vector3 vPos3, Quaternion qOrientation);
    void NavigatorBehave(float dt);

    void NavigatorLoad();
}