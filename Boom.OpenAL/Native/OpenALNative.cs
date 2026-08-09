using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Boom.OpenAL.Native;

/**
 * Raw OpenAL 1.1 entry points (WP-3.4).
 *
 * These replace Silk.NET.OpenAL's generated bindings. The native library itself is
 * unchanged - Karawan.Natives has supplied openal-soft since WP-1.5, and only the managed
 * binding is being replaced here.
 *
 * LIBRARY NAMING IS THE INTERESTING PART, and the one thing Silk was quietly doing for us.
 * The DllImport name below is a logical name; the resolver at the bottom maps it to whatever
 * the platform actually ships. Getting this wrong does not fail at build time - it throws
 * DllNotFoundException the first time a sound is played, which on a game is well after
 * startup looks healthy.
 */
internal static unsafe class OpenALNative
{
    /*
     * A logical name, never a filename. Every real name is in s_candidates.
     */
    private const string Lib = "openal";

    static OpenALNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(OpenALNative).Assembly, _resolve);
    }

    /**
     * Force the static constructor - and therefore the resolver registration - to have run.
     *
     * Called before the first P/Invoke. A DllImport does NOT trigger the declaring type's
     * static constructor reliably, so without this the resolver can be registered too late
     * and the very first call falls back to the default probing that this exists to replace.
     */
    internal static void EnsureResolverInstalled()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(
            typeof(OpenALNative).TypeHandle);
    }

    /*
     * Ordered by preference per platform.
     *
     * Windows carries BOTH names on purpose: WP-1.5 found that Karawan.Natives ships
     * OpenAL32.dll where Silk.NET.OpenAL.Soft.Native shipped soft_oal.dll, and that Silk
     * papered over the difference by trying several candidates. A single-name DllImport here
     * would have reintroduced exactly the breakage WP-1.5 diagnosed.
     *
     * Linux and macOS list the versioned SONAME first: a distro package usually installs
     * libopenal.so.1 and only provides the bare libopenal.so with its -dev package.
     */
    private static string[] _candidates()
    {
        if (OperatingSystem.IsWindows())
        {
            return new[] { "OpenAL32.dll", "soft_oal.dll", "openal32.dll", "OpenAL32" };
        }

        if (OperatingSystem.IsMacOS())
        {
            return new[] { "libopenal.1.dylib", "libopenal.dylib", "libopenal" };
        }

        // Linux and Android. Android packages it as a bare libopenal.so inside the APK.
        return new[] { "libopenal.so.1", "libopenal.so", "libopenal" };
    }

    private static IntPtr _resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != Lib)
        {
            return IntPtr.Zero;
        }

        foreach (string candidate in _candidates())
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out IntPtr handle))
            {
                return handle;
            }
        }

        /*
         * Returning Zero lets the runtime raise DllNotFoundException naming only the logical
         * name, which would be actively misleading - there is no file called "openal". Throw
         * with the list actually tried.
         */
        throw new DllNotFoundException(
            $"Could not load OpenAL. Tried: {string.Join(", ", _candidates())}. "
            + "The native library ships in the Karawan.Natives package.");
    }

    // ---- AL ------------------------------------------------------------------------------

    [DllImport(Lib, EntryPoint = "alGetError")]
    internal static extern AudioError alGetError();

    [DllImport(Lib, EntryPoint = "alGenBuffers")]
    internal static extern void alGenBuffers(int n, uint* buffers);

    [DllImport(Lib, EntryPoint = "alDeleteBuffers")]
    internal static extern void alDeleteBuffers(int n, uint* buffers);

    [DllImport(Lib, EntryPoint = "alBufferData")]
    internal static extern void alBufferData(uint buffer, int format, void* data, int size, int freq);

    [DllImport(Lib, EntryPoint = "alGenSources")]
    internal static extern void alGenSources(int n, uint* sources);

    [DllImport(Lib, EntryPoint = "alDeleteSources")]
    internal static extern void alDeleteSources(int n, uint* sources);

    [DllImport(Lib, EntryPoint = "alSourcePlay")]
    internal static extern void alSourcePlay(uint source);

    [DllImport(Lib, EntryPoint = "alSourceStop")]
    internal static extern void alSourceStop(uint source);

    [DllImport(Lib, EntryPoint = "alSourceQueueBuffers")]
    internal static extern void alSourceQueueBuffers(uint source, int n, uint* buffers);

    [DllImport(Lib, EntryPoint = "alSourcef")]
    internal static extern void alSourcef(uint source, int param, float value);

    [DllImport(Lib, EntryPoint = "alSourcei")]
    internal static extern void alSourcei(uint source, int param, int value);

    [DllImport(Lib, EntryPoint = "alSource3f")]
    internal static extern void alSource3f(uint source, int param, float v1, float v2, float v3);

    [DllImport(Lib, EntryPoint = "alListenerf")]
    internal static extern void alListenerf(int param, float value);

    [DllImport(Lib, EntryPoint = "alListener3f")]
    internal static extern void alListener3f(int param, float v1, float v2, float v3);

    [DllImport(Lib, EntryPoint = "alListenerfv")]
    internal static extern void alListenerfv(int param, float* values);

    [DllImport(Lib, EntryPoint = "alDistanceModel")]
    internal static extern void alDistanceModel(int distanceModel);

    // ---- ALC -----------------------------------------------------------------------------

    [DllImport(Lib, EntryPoint = "alcOpenDevice")]
    internal static extern Device* alcOpenDevice([MarshalAs(UnmanagedType.LPUTF8Str)] string? deviceName);

    [DllImport(Lib, EntryPoint = "alcCloseDevice")]
    internal static extern bool alcCloseDevice(Device* device);

    [DllImport(Lib, EntryPoint = "alcCreateContext")]
    internal static extern Context* alcCreateContext(Device* device, int* attrList);

    [DllImport(Lib, EntryPoint = "alcDestroyContext")]
    internal static extern void alcDestroyContext(Context* context);

    [DllImport(Lib, EntryPoint = "alcMakeContextCurrent")]
    internal static extern bool alcMakeContextCurrent(Context* context);

    [DllImport(Lib, EntryPoint = "alcProcessContext")]
    internal static extern void alcProcessContext(Context* context);

    [DllImport(Lib, EntryPoint = "alcGetCurrentContext")]
    internal static extern Context* alcGetCurrentContext();

    [DllImport(Lib, EntryPoint = "alcGetContextsDevice")]
    internal static extern Device* alcGetContextsDevice(Context* context);

    [DllImport(Lib, EntryPoint = "alcGetError")]
    internal static extern ContextError alcGetError(Device* device);

    [DllImport(Lib, EntryPoint = "alcIsExtensionPresent")]
    internal static extern bool alcIsExtensionPresent(
        Device* device, [MarshalAs(UnmanagedType.LPUTF8Str)] string extName);

    [DllImport(Lib, EntryPoint = "alcGetIntegerv")]
    internal static extern void alcGetIntegerv(Device* device, int param, int size, int* values);

    /*
     * Returns a pointer OpenAL owns. For the device-specifier tokens it is a sequence of
     * NUL-terminated strings ended by an empty one, NOT a single string - see
     * Enumeration.GetStringList.
     */
    [DllImport(Lib, EntryPoint = "alcGetString")]
    internal static extern byte* alcGetString(Device* device, int param);
}
