using System.Runtime.InteropServices;
using Android.App;
using Android.Content.PM;
using Org.Libsdl.App;

namespace Sdl3Spike;

/// <summary>
/// WP-2.1: the whole spike. Derives from SDL3's own <c>org.libsdl.app.SDLActivity</c>
/// (compiled from vendored Java source, see java/PROVENANCE.md) and runs a GLES 3.0
/// clear loop on the SDL thread.
/// </summary>
/// <remarks>
/// <para>
/// The control flow is not obvious, and it is the part WP-2.2 has to reproduce inside
/// Wuka, so it is spelled out here:
/// </para>
/// <list type="number">
/// <item>Android starts this activity. <see cref="LoadLibraries"/> runs first (SDL's Java
/// calls it before anything else) and we hand libmain.so a function pointer to
/// <see cref="SdlMain"/>.</item>
/// <item>SDL's Java glue creates the surface, then spawns its own "SDLThread" and calls
/// <c>nativeRunMain("libmain.so", "SDL_main", ...)</c>.</item>
/// <item>libmain.so's <c>SDL_main</c> invokes the pointer from step 1 - so
/// <see cref="SdlMain"/> runs <b>on SDL's thread, not the Android UI thread</b>.
/// Everything SDL-related must happen there.</item>
/// </list>
/// <para>
/// This is the same bridge Silk.NET used; WP-0.0 recovered the shim by disassembly and
/// recipes/build-mainshim.sh rebuilds it. See that script for the alternative
/// (overriding SDLActivity.main() in C# and dropping libmain.so entirely), which is
/// cleaner but was not worth the risk inside a timeboxed spike.
/// </para>
/// </remarks>
[Activity(
    Label = "SDL3 Spike",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation
                           | ConfigChanges.KeyboardHidden | ConfigChanges.Keyboard,
    Theme = "@android:style/Theme.Black.NoTitleBar.Fullscreen")]
public class SdlSpikeActivity : SDLActivity
{
    private const string Tag = "SDL3SPIKE";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MainFunc();

    /// <summary>Registers the managed entry point with libmain.so.</summary>
    /// <remarks>
    /// The library name is spelled "libmain.so" in full, not "main". Both usually
    /// resolve, but this is the exact spelling Silk.NET shipped and it is the one
    /// proven to work on Android's loader - not worth being clever about inside a
    /// spike whose failure mode is a hang with no output.
    /// </remarks>
    [DllImport("libmain.so", EntryPoint = "sdSetMain")]
    private static extern void SetupMain(IntPtr funcPtr);

    // Kept alive for the process lifetime. If this were a local, the GC would be free
    // to collect the delegate while native code still holds its address - a crash that
    // happens minutes later and looks like an SDL bug.
    private static readonly MainFunc s_mainDelegate = SdlMain;
    private static IntPtr s_mainPtr;

    private static SdlSpikeActivity s_instance;

    public override void LoadLibraries()
    {
        base.LoadLibraries();

        s_instance = this;
        if (s_mainPtr == IntPtr.Zero)
        {
            s_mainPtr = Marshal.GetFunctionPointerForDelegate(s_mainDelegate);
            SetupMain(s_mainPtr);
        }
    }

    /// <summary>
    /// Runs on SDL's thread. Returning from here ends the app, so the render loop lives
    /// inside it.
    /// </summary>
    private static void SdlMain()
    {
        try
        {
            SpikeRenderer.Run(msg => Android.Util.Log.Info(Tag, msg));
        }
        catch (Exception e)
        {
            // Without this the exception unwinds into native SDL code and the process
            // dies with no usable message - which during a spike is indistinguishable
            // from "SDL3 does not work on Android".
            Android.Util.Log.Error(Tag, "spike failed: " + e);
        }
        finally
        {
            s_instance = null;
        }
    }
}
