using System.Numerics;
using MessagePack;
using Xunit;

namespace JoyceCode.Tests.engine.joyce;

/**
 * WP-4.1 spike probe.
 *
 * The tree contains no IMessagePackFormatter and no custom resolver, yet
 * ModelAnimationCollection serialises Matrix4x4[] today and the ac-{hash} files
 * load. Before annotating anything else, establish WHICH System.Numerics types
 * the standard resolver actually accepts, rather than inferring it from the fact
 * that one of them happens to work.
 */
public class MessagePackProbeTests
{
    private static readonly MessagePackSerializerOptions _options =
        MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);

    private static T RoundTrip<T>(T value)
    {
        var bytes = MessagePackSerializer.Serialize(value, _options);
        return MessagePackSerializer.Deserialize<T>(bytes, _options);
    }

    [Fact]
    public void Matrix4x4RoundTrips()
    {
        var m = Matrix4x4.CreateRotationY(0.5f) * Matrix4x4.CreateTranslation(1f, 2f, 3f);
        Assert.Equal(m, RoundTrip(m));
    }

    [Fact]
    public void Vector3RoundTrips()
    {
        var v = new Vector3(1f, -2.5f, 3.25f);
        Assert.Equal(v, RoundTrip(v));
    }

    [Fact]
    public void Vector2RoundTrips()
    {
        var v = new Vector2(0.5f, 0.25f);
        Assert.Equal(v, RoundTrip(v));
    }

    [Fact]
    public void Vector4RoundTrips()
    {
        var v = new Vector4(1f, 2f, 3f, 4f);
        Assert.Equal(v, RoundTrip(v));
    }

    [Fact]
    public void QuaternionRoundTrips()
    {
        var q = Quaternion.CreateFromYawPitchRoll(0.1f, 0.2f, 0.3f);
        Assert.Equal(q, RoundTrip(q));
    }

    [Fact]
    public void Int4RoundTrips()
    {
        var i4 = new global::engine.joyce.Int4 { B0 = 1, B1 = 2, B2 = 3, B3 = 4 };
        var o = RoundTrip(i4);
        Assert.Equal(i4.B0, o.B0);
        Assert.Equal(i4.B1, o.B1);
        Assert.Equal(i4.B2, o.B2);
        Assert.Equal(i4.B3, o.B3);
    }
}
