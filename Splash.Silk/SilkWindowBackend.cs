using System;
using System.Numerics;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace Splash.Silk;

/// <summary>
/// <see cref="IWindowBackend"/> over a Silk <see cref="IView"/> - the desktop path, and the
/// one that must not change behaviour.
/// </summary>
/// <remarks>
/// Every quirk here was in <c>Platform</c> before WP-3.3 and is preserved deliberately,
/// including the hand-rolled loop below. This adapter is meant to be boring: if desktop
/// rendering or input differs after this refactor, the bug is in this file.
/// </remarks>
public sealed class SilkWindowBackend : IWindowBackend
{
    private readonly IView _iView;
    private IInputContext? _iInputContext;

    public SilkWindowBackend(IView iView)
    {
        _iView = iView ?? throw new ArgumentNullException(nameof(iView));
    }

    /// <summary>
    /// The underlying view. Only for the few desktop-only features that genuinely need a
    /// Silk type - today the ImGui controller, which takes an <c>IView</c> in its public
    /// API and therefore cannot be abstracted away until ImGui itself is dealt with.
    /// </summary>
    public IView View => _iView;

    public Vector2 Size => new(_iView.Size.X, _iView.Size.Y);

    public Vector2 FramebufferSize => new(_iView.FramebufferSize.X, _iView.FramebufferSize.Y);

    public Func<string, nint> GetProcAddress =>
        name => _iView.GLContext!.TryGetProcAddress(name, out nint addr) ? addr : 0;

    public bool IsClosing => _iView.IsClosing;

    public IInputContext? SilkInputContext => _iInputContext;

    /// <summary>
    /// Same determination as before WP-3.3, made once here rather than by string-matching
    /// the view's type name at the call site.
    /// </summary>
    public Platform.UnderlyingFrameworks UnderlyingFramework
    {
        get
        {
            string? name = _iView.GetType().FullName;
            if (null == name) return Platform.UnderlyingFrameworks.Unknown;
            if (name.Contains("Glfw")) return Platform.UnderlyingFrameworks.Glfw;
            if (name.Contains("Sdl")) return Platform.UnderlyingFrameworks.Sdl;
            return Platform.UnderlyingFrameworks.Unknown;
        }
    }

    public Action? OnLoad { get; set; }
    public Action<Vector2>? OnResize { get; set; }
    public Action<double>? OnUpdate { get; set; }
    public Action<double>? OnRender { get; set; }
    public Action? OnClosing { get; set; }
    public Action<bool>? OnFocusChanged { get; set; }
    public Action? BeforeEvents { get; set; }
    public Action? ReleaseMainThreadWaiters { get; set; }

    // Unused here: this backend has a real Silk input context, so Platform subscribes to
    // its keyboards directly, exactly as it did before WP-3.3.
    public Action<string>? OnKeyPressed { get; set; }
    public Action<string>? OnKeyReleased { get; set; }
    public Action<string>? OnKeyCharacter { get; set; }

    /*
     * Declared but never invoked, exactly as the three above are. This backend HAS a Silk
     * input context, so Platform subscribes to Silk's own events directly and these stay
     * null - they exist for backends that must translate raw platform events themselves.
     */
    public Action<Vector2>? OnMouseMoved { get; set; }
    public Action<Vector2, Vector2>? OnMouseWheel { get; set; }
    public Action<int, Vector2>? OnMousePressed { get; set; }
    public Action<int, Vector2>? OnMouseReleased { get; set; }
    public Action<int, Vector2>? OnGamepadStickMoved { get; set; }
    public Action<int, float>? OnGamepadTriggerMoved { get; set; }
    public Action<string, uint>? OnGamepadButtonPressed { get; set; }
    public Action<string, uint>? OnGamepadButtonReleased { get; set; }

    /// <summary>
    /// Wire Silk's events through to the callbacks. Called by <c>Platform.SetupDone</c>, at
    /// the same point the old code did <c>_iView.Load += ...</c>.
    /// </summary>
    public void Subscribe()
    {
        _iView.Load += () =>
        {
            // Silk hands the input context out only after Load; creating it earlier
            // returns a context with no devices attached.
            _iInputContext = _iView.CreateInput();
            OnLoad?.Invoke();
        };
        _iView.Resize += size => OnResize?.Invoke(new Vector2(size.X, size.Y));
        _iView.Render += dt => OnRender?.Invoke(dt);
        _iView.Update += dt => OnUpdate?.Invoke(dt);
        _iView.Closing += () => OnClosing?.Invoke();
        _iView.FocusChanged += focus => OnFocusChanged?.Invoke(focus);
    }

    public void SwapBuffers() => _iView.SwapBuffers();

    /// <summary>
    /// Moved verbatim from <c>Platform._setKeyboardEnabled</c>, where it used to be the
    /// whole body.
    /// </summary>
    /// <remarks>
    /// The null guard is new and matters: <c>Platform</c> called this unconditionally, so
    /// once a backend could have no Silk input context (WP-3.3) the original was a latent
    /// NRE on every non-Silk backend. See KI-10.
    /// </remarks>
    public void SetKeyboardVisible(bool isVisible)
    {
        if (null == _iInputContext)
        {
            return;
        }

        for (int i = 0; i < _iInputContext.Keyboards.Count; i++)
        {
            if (isVisible)
            {
                _iInputContext.Keyboards[i].BeginInput();
            }
            else
            {
                _iInputContext.Keyboards[i].EndInput();
            }
        }
    }

    /// <summary>
    /// Moved verbatim from <c>Platform.SetFullscreen</c>, including the explicit resize and
    /// the swallowed exception.
    /// </summary>
    /// <remarks>
    /// Preserved rather than tidied: known issue KI-1 is that a Release build hangs on
    /// fullscreen, so this code is already under suspicion. Changing it during a windowing
    /// refactor would make that investigation impossible to bisect.
    /// </remarks>
    public void SetFullscreen(bool isFullscreen)
    {
        // Only a real window can go fullscreen; an embedded view cannot.
        if (_iView is not IWindow iWindow)
        {
            return;
        }

        try
        {
            if (isFullscreen)
            {
                iWindow.Size = new Vector2D<int>(1280, 720);
                iWindow.WindowState = WindowState.Fullscreen;
            }
            else
            {
                //iWindow.Size = new Vector2D<int>(1280, 720);
                iWindow.WindowState = WindowState.Normal;
            }
        }
        catch (Exception)
        {
            engine.Logger.Error($"Exception while setting fullscreen to {isFullscreen}");
            // TXWTODO: FIx exception when de-fullscreening.
            // Setting fullscreeen might fail.
        }
    }

    public void Run()
    {
        /*
         * Instead of just calling _iView.Run(), we run the same thing explicitly. That way
         * we can inject calls. (Verbatim from the pre-WP-3.3 Platform - the injection point
         * is BeforeEvents, which Wuka uses.)
         */
        IView iView = _iView;
        iView.Run(delegate
        {
            ReleaseMainThreadWaiters?.Invoke();

            BeforeEvents?.Invoke();

            try
            {
                iView.DoEvents();
            }
            catch (Exception)
            {
                /*
                 * Catching exception that might come from unknown keys in ImLib
                 */
            }

            if (!iView.IsClosing)
            {
                ReleaseMainThreadWaiters?.Invoke();
                iView.DoUpdate();
            }

            if (!iView.IsClosing)
            {
                iView.DoRender();
            }
        });

        iView.DoEvents();

        ReleaseMainThreadWaiters?.Invoke();

        iView.Reset();
    }

    public void Close() => _iView.Close();

    public void Dispose() => _iView.Dispose();
}
