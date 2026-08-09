using System;
using System.Numerics;

namespace Boom.OpenAL.Native;

/**
 * Drop-in replacement for Silk.NET.OpenAL's AL class (WP-3.4).
 *
 * Every method name and signature here exists because a call site in Boom.OpenAL uses it.
 * The shapes are Silk's, so the call sites did not change - only the namespace they import.
 * That is the whole point: a binding swap should not be a rewrite of the audio code, and
 * anything that behaves differently afterwards is then a bug in THIS file rather than
 * somewhere in ninety-two edits.
 */
public unsafe class AL
{
    private AL()
    {
        OpenALNative.EnsureResolverInstalled();
    }

    /// <summary>Mirrors <c>Silk.NET.OpenAL.AL.GetApi()</c>.</summary>
    /// <remarks>
    /// There is no API object to fetch - these are plain exported functions - so this exists
    /// only so <c>API.cs</c> keeps reading the way it did.
    /// </remarks>
    public static AL GetApi() => new AL();

    public AudioError GetError() => OpenALNative.alGetError();

    public uint GenBuffer()
    {
        uint buffer = 0;
        OpenALNative.alGenBuffers(1, &buffer);
        return buffer;
    }

    public void DeleteBuffer(uint buffer) => OpenALNative.alDeleteBuffers(1, &buffer);

    public uint GenSource()
    {
        uint source = 0;
        OpenALNative.alGenSources(1, &source);
        return source;
    }

    public void DeleteSource(uint source) => OpenALNative.alDeleteSources(1, &source);

    /// <summary>
    /// Uploads 16-bit PCM samples. <paramref name="frequency"/> is the sample rate in Hz.
    /// </summary>
    /// <remarks>
    /// Silk's overload is generic over the sample type; only the <c>short[]</c> instantiation
    /// is used here (OGGSound decodes to 16-bit), so only that one exists. The byte count is
    /// computed from the array length - passing a sample count where OpenAL wants BYTES
    /// yields audio that plays at half length with no error reported.
    /// </remarks>
    public void BufferData(uint buffer, BufferFormat format, short[] data, int frequency)
    {
        if (null == data || 0 == data.Length)
        {
            return;
        }

        fixed (short* pData = data)
        {
            OpenALNative.alBufferData(
                buffer, (int)format, pData, data.Length * sizeof(short), frequency);
        }
    }

    public void SourceQueueBuffers(uint source, uint[] buffers)
    {
        if (null == buffers || 0 == buffers.Length)
        {
            return;
        }

        fixed (uint* pBuffers = buffers)
        {
            OpenALNative.alSourceQueueBuffers(source, buffers.Length, pBuffers);
        }
    }

    public void SourcePlay(uint source) => OpenALNative.alSourcePlay(source);

    public void SourceStop(uint source) => OpenALNative.alSourceStop(source);

    public void SetSourceProperty(uint source, SourceFloat param, float value)
        => OpenALNative.alSourcef(source, (int)param, value);

    public void SetSourceProperty(uint source, SourceVector3 param, Vector3 value)
        => OpenALNative.alSource3f(source, (int)param, value.X, value.Y, value.Z);

    public void SetSourceProperty(uint source, SourceVector3 param, float x, float y, float z)
        => OpenALNative.alSource3f(source, (int)param, x, y, z);

    /// <remarks>
    /// OpenAL has no boolean setter; <c>alSourcei</c> with 1/0 is how AL_LOOPING is set, and
    /// is what Silk did too.
    /// </remarks>
    public void SetSourceProperty(uint source, SourceBoolean param, bool value)
        => OpenALNative.alSourcei(source, (int)param, value ? 1 : 0);

    public void SetListenerProperty(ListenerFloat param, float value)
        => OpenALNative.alListenerf((int)param, value);

    public void SetListenerProperty(ListenerVector3 param, Vector3 value)
        => OpenALNative.alListener3f((int)param, value.X, value.Y, value.Z);

    /// <summary>
    /// Listener orientation: six floats, front (xyz) then up (xyz).
    /// </summary>
    /// <remarks>
    /// The caller pins its own array and passes the pointer, exactly as it did against Silk
    /// - see <c>UpdateMovingSoundsSystem.PreUpdate</c>. OpenAL reads six floats and there is
    /// no length parameter, so a shorter array reads past its end without complaint.
    /// </remarks>
    public void SetListenerProperty(ListenerFloatArray param, float* values)
        => OpenALNative.alListenerfv((int)param, values);

    public void DistanceModel(DistanceModel distanceModel)
        => OpenALNative.alDistanceModel((int)distanceModel);
}
