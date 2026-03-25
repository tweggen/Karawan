using System;

namespace engine.navigation;

/// <summary>
/// Enumeration of transportation types.
/// Used as flags to indicate which types can use a lane.
/// </summary>
[Flags]
public enum TransportationType
{
    Pedestrian = 1,
    Car = 2,
    Bicycle = 4,
    Bus = 8,
    // Future: Motorcycle = 16, Truck = 32, etc.
}

/// <summary>
/// Wrapper for TransportationType flags with convenience methods.
/// </summary>
public class TransportationTypeFlags
{
    public TransportationType Value { get; set; }

    public TransportationTypeFlags()
    {
        Value = TransportationType.Pedestrian;
    }

    public TransportationTypeFlags(TransportationType initialValue)
    {
        Value = initialValue;
    }

    /// <summary>
    /// Check if this type is allowed.
    /// </summary>
    public bool HasFlag(TransportationType type)
    {
        return (Value & type) != 0;
    }

    /// <summary>
    /// Add a type to the flags.
    /// </summary>
    public void Add(TransportationType type)
    {
        Value |= type;
    }

    /// <summary>
    /// Remove a type from the flags.
    /// </summary>
    public void Remove(TransportationType type)
    {
        Value &= ~type;
    }

    /// <summary>
    /// Clear all flags.
    /// </summary>
    public void Clear()
    {
        Value = 0;
    }

    public override string ToString() => Value.ToString();
}
