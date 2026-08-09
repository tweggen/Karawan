using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Boom.OpenAL.Native;

/**
 * Drop-in replacement for Silk.NET.OpenAL's ALContext (WP-3.4). See AL for the rationale.
 */
public unsafe class ALContext
{
    private ALContext()
    {
        OpenALNative.EnsureResolverInstalled();
    }

    /// <summary>Mirrors <c>Silk.NET.OpenAL.ALContext.GetApi(bool)</c>.</summary>
    /// <remarks>
    /// <c>API.cs</c> calls <c>GetApi(true)</c> and falls back to <c>GetApi()</c> in a catch
    /// block. In Silk the flag selected the soft_oal build; here the resolver already tries
    /// every candidate name, so both forms do the same thing. The parameter is kept so the
    /// existing call and its fallback keep compiling and reading sensibly.
    /// </remarks>
    public static ALContext GetApi(bool preferSoft = false) => new ALContext();

    public Device* OpenDevice(string? deviceName)
    {
        /*
         * OpenAL treats NULL as "the default device", and an EMPTY string as a device named
         * "" - which does not exist, so it fails. _researchDeviceName returns "" when
         * enumeration found nothing, so the two must be collapsed here or the default device
         * is never opened on a machine without ALC_ENUMERATION_EXT.
         */
        return OpenALNative.alcOpenDevice(string.IsNullOrEmpty(deviceName) ? null : deviceName);
    }

    public bool CloseDevice(Device* device) => OpenALNative.alcCloseDevice(device);

    public Context* CreateContext(Device* device, int* attrList)
        => OpenALNative.alcCreateContext(device, attrList);

    public void DestroyContext(Context* context) => OpenALNative.alcDestroyContext(context);

    public bool MakeContextCurrent(Context* context) => OpenALNative.alcMakeContextCurrent(context);

    public void ProcessContext(Context* context) => OpenALNative.alcProcessContext(context);

    public Context* GetCurrentContext() => OpenALNative.alcGetCurrentContext();

    public Device* GetContextsDevice(Context* context) => OpenALNative.alcGetContextsDevice(context);

    public ContextError GetError(Device* device) => OpenALNative.alcGetError(device);

    public bool IsExtensionPresent(Device* device, string extensionName)
        => OpenALNative.alcIsExtensionPresent(device, extensionName);

    public void GetContextProperty(Device* device, GetContextInteger param, int size, int* values)
        => OpenALNative.alcGetIntegerv(device, (int)param, size, values);

    /// <summary>
    /// Mirrors Silk's extension accessor. Only <see cref="Enumeration"/> is used.
    /// </summary>
    public T GetExtension<T>(Device* device) where T : class
        => (T)(object)new Enumeration(device);
}


/**
 * ALC_ENUMERATION_EXT - listing the available output devices.
 *
 * Disposable purely so the `using var enumeration = ...` in API.cs keeps compiling; there is
 * nothing to release, because the strings belong to OpenAL.
 */
public unsafe class Enumeration : IDisposable
{
    private readonly Device* _device;

    internal Enumeration(Device* device)
    {
        _device = device;
    }

    /// <summary>
    /// The device specifiers, as a list.
    /// </summary>
    /// <remarks>
    /// <b>This is not a string.</b> <c>alcGetString</c> returns a sequence of NUL-terminated
    /// strings terminated by an empty one, so reading it with the usual
    /// <c>Marshal.PtrToStringUTF8</c> yields only the FIRST device and silently hides every
    /// other one. Walk it until the double NUL.
    /// </remarks>
    public IEnumerable<string> GetStringList(GetEnumerationContextStringList param)
    {
        var result = new List<string>();

        byte* p = OpenALNative.alcGetString(_device, (int)param);
        if (null == p)
        {
            return result;
        }

        while (*p != 0)
        {
            string? entry = Marshal.PtrToStringUTF8((IntPtr)p);
            if (string.IsNullOrEmpty(entry))
            {
                break;
            }

            result.Add(entry);

            // Step past this entry and its NUL.
            while (*p != 0)
            {
                p++;
            }

            p++;
        }

        return result;
    }

    public void Dispose()
    {
    }
}
