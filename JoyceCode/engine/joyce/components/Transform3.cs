using System.Numerics;
using System.Text.Json.Serialization;

namespace engine.joyce.components;


/**
 * Keeps user definable parameters for the current transformation
 * state of an object.
 *
 * This is used if you transform an object manually by means of the
 * transform API. Every time you change a property here, the matrix
 * in Transform3ToParent will be updated.
 *
 * After the run of the PropagateTranmslationSystem, the Transform3ToParent
 * will be merged into the Transform3ToWorld components.
 *
 * That means, to be totally efficient, you should avoid to manually
 * set the positiions using the API. I mean, if you're in the thousands
 * of entities.
 */
[engine.IsPersistable]
public struct Transform3
{
    [JsonInclude]
    public bool IsVisible;
    [JsonInclude]
    public uint CameraMask;
    [JsonInclude]
    public Quaternion Rotation;
    [JsonInclude]
    public Vector3 Position;
    [JsonInclude]
    public Vector3 Scale;


    public override string ToString()
    {
        return
            $"IsVisible={IsVisible}, CameraMask={CameraMask:X}, Rotation={Rotation}, Scale={Scale}, Position={Position}";
    }


    public Transform3(bool isVisible, uint cameraMask, in Quaternion rotation, in Vector3 position)
    {
        IsVisible = isVisible;
        CameraMask = cameraMask;
        Rotation = rotation;
        Position = position;
        Scale = Vector3.One;
    }


    public Transform3(bool isVisible, uint cameraMask, in Quaternion rotation, in Vector3 position, in Vector3 scale)
    {
        IsVisible = isVisible;
        CameraMask = cameraMask;
        Rotation = rotation;
        Position = position;
        Scale = scale;
    }
}