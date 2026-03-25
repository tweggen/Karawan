using Xunit;
using engine.navigation;

namespace JoyceCode.Tests.engine.navigation;

public class TransportationTypeTests
{
    [Fact]
    public void TransportationTypeFlags_HasFlag_ReturnsTrueForAddedType()
    {
        var flags = new TransportationTypeFlags();
        flags.Add(TransportationType.Car);

        Assert.True(flags.HasFlag(TransportationType.Car));
    }

    [Fact]
    public void TransportationTypeFlags_HasFlag_ReturnsFalseForMissingType()
    {
        var flags = new TransportationTypeFlags();

        Assert.False(flags.HasFlag(TransportationType.Car));
    }

    [Fact]
    public void TransportationTypeFlags_Remove_RemovesType()
    {
        var flags = new TransportationTypeFlags(
            TransportationType.Pedestrian | TransportationType.Car);

        flags.Remove(TransportationType.Car);

        Assert.True(flags.HasFlag(TransportationType.Pedestrian));
        Assert.False(flags.HasFlag(TransportationType.Car));
    }

    [Fact]
    public void TransportationTypeFlags_Clear_RemovesAllTypes()
    {
        var flags = new TransportationTypeFlags(
            TransportationType.Pedestrian | TransportationType.Car | TransportationType.Bicycle);

        flags.Clear();

        Assert.False(flags.HasFlag(TransportationType.Pedestrian));
        Assert.False(flags.HasFlag(TransportationType.Car));
        Assert.False(flags.HasFlag(TransportationType.Bicycle));
    }

    [Fact]
    public void TransportationTypeFlags_Constructor_InitializesWithValue()
    {
        var flags = new TransportationTypeFlags(TransportationType.Car);

        Assert.True(flags.HasFlag(TransportationType.Car));
        Assert.False(flags.HasFlag(TransportationType.Pedestrian));
    }

    [Fact]
    public void TransportationTypeFlags_DefaultConstructor_InitializesPedestrian()
    {
        var flags = new TransportationTypeFlags();

        Assert.True(flags.HasFlag(TransportationType.Pedestrian));
    }

    [Fact]
    public void TransportationTypeFlags_ToString_ReturnsValueString()
    {
        var flags = new TransportationTypeFlags(TransportationType.Car);

        var result = flags.ToString();

        Assert.NotEmpty(result);
        Assert.Contains("Car", result);
    }

    [Fact]
    public void TransportationTypeFlags_MultipleTypes_WorksCorrectly()
    {
        var flags = new TransportationTypeFlags();
        flags.Add(TransportationType.Car);
        flags.Add(TransportationType.Bus);
        flags.Add(TransportationType.Pedestrian);

        Assert.True(flags.HasFlag(TransportationType.Car));
        Assert.True(flags.HasFlag(TransportationType.Bus));
        Assert.True(flags.HasFlag(TransportationType.Pedestrian));
        Assert.False(flags.HasFlag(TransportationType.Bicycle));
    }
}
