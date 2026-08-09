using Android.Content;
using Android.Views;
using Android.Views.InputMethods;
using static engine.Logger;
using View = Android.Views.View;

namespace Wuka;

/**
 * Raises and dismisses the Android on-screen keyboard for the game surface.
 *
 * Installed onto Sdl3WindowBackend.SoftKeyboardHandler by GameActivity, because
 * Splash.Silk cannot reference Android APIs. Reached from
 * builtin/jt/InputWidgetImplementation -> Engine.EnableKeyboard ->
 * IPlatform.KeyboardEnabled -> Platform._setKeyboardEnabled -> the backend seam.
 *
 * WHY NOT SDL_StartTextInput
 * --------------------------
 * Because it would work, and then quietly break text entry. SDL3's
 * SDLActivity.showTextInput() builds its own SDLDummyEdit view, calls requestFocus()
 * on it and shows the keyboard against that view - so the IME binds to SDL's
 * SDLInputConnection, not to GameSurface.OnCreateInputConnection. SDL then translates
 * IME commitText/setComposingText into backspace+retype sequences, and because the
 * engine's InputWidget does not track the composing region, those backspaces eat into
 * already-committed text: after accepting an autocorrect suggestion, characters appear
 * at position 0 in reverse order. That is the exact defect KarawanInputConnection was
 * written to fix (docs/SYSTEMS/PLATFORMS/ANDROID.md, "IME Composition Bug (Fixed)").
 *
 * Binding the IME to the surface ourselves keeps focus - and therefore the
 * InputConnection - where the ported code already expects it, and costs nothing: the
 * surface is focusable and already focused, since SDLSurface's constructor calls
 * setFocusable(true) / setFocusableInTouchMode(true) / requestFocus().
 */
public static class AndroidSoftKeyboard
{
    private static readonly engine.Dc _dc = engine.Dc.Input;

    /**
     * Show or hide the keyboard for the given surface.
     *
     * Called on SDL's thread, never the Android UI thread - Platform queues this onto its
     * platform-thread action queue, and that thread is the one SDL_main runs on.
     * InputMethodManager must be touched from the UI thread, so every call is posted to
     * the view. SDL does the same thing for the same reason (ShowTextInputTask is posted
     * through SDLActivity's commandHandler).
     */
    public static void SetVisible(View surface, bool isVisible)
    {
        if (null == surface)
        {
            Warning("No game surface; cannot change keyboard visibility.");
            return;
        }

        /*
         * Post returns false when the view is not attached to a window, and silently drops
         * the runnable. Worth a line: the symptom would otherwise be "the keyboard just
         * does not appear", with nothing at all in the log to say why.
         */
        bool posted = surface.Post(() =>
        {
            try
            {
                var imm = (InputMethodManager?)surface.Context?.GetSystemService(Context.InputMethodService);
                if (null == imm)
                {
                    Warning("No InputMethodManager; cannot change keyboard visibility.");
                    return;
                }

                if (isVisible)
                {
                    /*
                     * RequestFocus first: OnCreateInputConnection is only called for the
                     * focused view, and focus can have moved.
                     */
                    surface.RequestFocus();

                    /*
                     * RestartInput, then show. The IME binds once and caches the EditorInfo
                     * it was given, but GameSurface.OnCreateInputConnection chooses
                     * InputType from Engine.KeyboardInputType - which differs per widget
                     * ("text", "email", "password", "number"). Without this, the second
                     * field to gain focus gets the first field's keyboard.
                     */
                    imm.RestartInput(surface);

                    /*
                     * Flags 0, matching SDL's own showSoftInput(mTextEdit, 0) - NOT
                     * ShowFlags.Implicit, which lets the system ignore the request if the
                     * user dismissed the keyboard earlier. This call is already a direct
                     * response to the user focusing a text field, so it should not be
                     * second-guessed. The enum has no None member, hence the cast.
                     */
                    imm.ShowSoftInput(surface, (ShowFlags)0);

                    Trace(_dc, $"Keyboard shown for {surface.GetType().Name}.");
                }
                else
                {
                    imm.HideSoftInputFromWindow(surface.WindowToken, HideSoftInputFlags.None);

                    Trace(_dc, $"Keyboard hidden for {surface.GetType().Name}.");
                }
            }
            catch (System.Exception e)
            {
                /*
                 * Never let this kill the UI thread. A keyboard that fails to appear is a
                 * bad but survivable state; an exception here is not.
                 */
                Warning($"Failed to set keyboard visibility to {isVisible}: {e}");
            }
        });

        if (!posted)
        {
            Warning($"Could not post keyboard visibility {isVisible}: surface not attached to a window.");
        }
    }
}
