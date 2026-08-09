namespace Boom.OpenAL.Native;

/*
 * WP-3.4: the enums Boom.OpenAL used from Silk.NET.OpenAL, with the same NAMES and the same
 * underlying values.
 *
 * Names match Silk's deliberately, so the ~92 call sites keep compiling untouched - the only
 * change at a call site is which namespace it imports. Values are the OpenAL 1.1 constants
 * from AL/al.h and AL/alc.h, which is what Silk's own enums carry; they are ABI, not a
 * convention, so they cannot drift.
 */

/**
 * The full member set of Silk's AudioError, aliases included.
 *
 * OpenAL's headers define AL_ILLEGAL_ENUM and AL_ILLEGAL_COMMAND as legacy synonyms of
 * AL_INVALID_ENUM and AL_INVALID_OPERATION, so those pairs share a value. Both spellings are
 * kept because OGGSound.CheckError switches on IllegalCommand.
 *
 * ORDER MATTERS for the alias pairs: Enum.ToString returns the FIRST name declared for a
 * value, and these errors are logged by name. IllegalEnum and IllegalCommand are declared
 * first so log lines keep reading exactly as they did under Silk - "AL ERROR:
 * (IllegalCommand)" - which is what any existing log or search expects to find.
 */
public enum AudioError
{
    NoError = 0,
    InvalidName = 0xA001,
    IllegalEnum = 0xA002,
    InvalidEnum = 0xA002,
    InvalidValue = 0xA003,
    IllegalCommand = 0xA004,
    InvalidOperation = 0xA004,
    OutOfMemory = 0xA005
}

public enum ContextError
{
    NoError = 0,
    InvalidDevice = 0xA001,
    InvalidContext = 0xA002,
    InvalidEnum = 0xA003,
    InvalidValue = 0xA004,
    OutOfMemory = 0xA005
}

public enum BufferFormat
{
    Mono8 = 0x1100,
    Mono16 = 0x1101,
    Stereo8 = 0x1102,
    Stereo16 = 0x1103
}

public enum SourceFloat
{
    Pitch = 0x1003,
    Gain = 0x100A,
    MinGain = 0x100D,
    MaxGain = 0x100E,
    MaxDistance = 0x1023,
    RolloffFactor = 0x1021,
    ConeOuterGain = 0x1022,
    ConeInnerAngle = 0x1001,
    ConeOuterAngle = 0x1002,
    ReferenceDistance = 0x1020,
    SecOffset = 0x1024
}

public enum SourceVector3
{
    Position = 0x1004,
    Velocity = 0x1006,
    Direction = 0x1005
}

public enum SourceBoolean
{
    SourceRelative = 0x202,
    Looping = 0x1007
}

public enum ListenerFloat
{
    Gain = 0x100A
}

public enum ListenerVector3
{
    Position = 0x1004,
    Velocity = 0x1006
}

public enum ListenerFloatArray
{
    Orientation = 0x100F
}

public enum DistanceModel
{
    None = 0,
    InverseDistance = 0xD001,
    InverseDistanceClamped = 0xD002,
    LinearDistance = 0xD003,
    LinearDistanceClamped = 0xD004,
    ExponentDistance = 0xD005,
    ExponentDistanceClamped = 0xD006
}

public enum GetContextInteger
{
    MajorVersion = 0x1000,
    MinorVersion = 0x1001,
    AttributesSize = 0x1002,
    AllAttributes = 0x1003
}

public enum GetEnumerationContextStringList
{
    DeviceSpecifiers = 0x1005,
    CaptureDeviceSpecifiers = 0x310,
    AllDeviceSpecifiers = 0x1013
}

/*
 * Opaque handles. OpenAL never exposes their contents; only the pointer is meaningful, which
 * is why these are empty structs used solely as Device* / Context* - the same shape Silk
 * used, so `Device* _alDevice` and `Context* _currentContext` in API.cs are unchanged.
 */
public struct Device
{
}

public struct Context
{
}
