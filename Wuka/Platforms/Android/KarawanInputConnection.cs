using Android.Runtime;
using Android.Views;
using Android.Views.InputMethods;
using engine;
using engine.news;
using Java.Lang;
using View = Android.Views.View;

namespace Wuka;

/**
 * Custom InputConnection handling Android IME composition for the game surface.
 *
 * Instead of letting the IME translate composition into backspace + character
 * sequences (which corrupts our InputWidget's cursor position), we intercept
 * commitText/setComposingText and emit INPUT_TEXT_REPLACE events.
 *
 * Note this must NOT wrap a connection obtained from SDLSurface: SDLSurface is a
 * plain SurfaceView and does not implement onCreateInputConnection, so
 * View.onCreateInputConnection() returns null by contract (SDL routes its own IME
 * through the separate DummyEdit view). We therefore derive from BaseInputConnection,
 * which supplies working default behaviour for every method we do not override —
 * including sendKeyEvent(), which dispatches the key event to the target view and
 * hence into SDL's key handling, exactly as before.
 */
public class KarawanInputConnection : BaseInputConnection
{
    private int _composingLength = 0;


    public KarawanInputConnection(View targetView)
        : base(targetView, /* fullEditor: */ false)
    {
    }


    protected KarawanInputConnection(System.IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }


    public override bool CommitText(ICharSequence text, int newCursorPosition)
    {
        /*
         * The event queue only exists once the engine is up. Until then we have
         * nowhere to deliver text to, so silently drop it rather than throwing
         * across the JNI boundary.
         */
        var eq = I.TryGet<EventQueue>();
        if (eq != null)
        {
            /*
             * Delete the composing region and insert the committed text atomically.
             */
            eq.Push(new Event(Event.INPUT_TEXT_REPLACE, text?.ToString() ?? "")
            {
                Data1 = (uint)_composingLength
            });
        }

        _composingLength = 0;
        return true;
    }


    public override bool SetComposingText(ICharSequence text, int newCursorPosition)
    {
        /*
         * No composition preview — just track the composing length.
         * The text will be shown after commit.
         */
        _composingLength = text?.Length() ?? 0;
        return true;
    }


    public override bool FinishComposingText()
    {
        _composingLength = 0;
        return true;
    }


    public override bool SetComposingRegion(int start, int end)
    {
        _composingLength = System.Math.Max(0, end - start);
        return true;
    }


    public override bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        /*
         * This is a real text deletion (not composing region management).
         * Emit backspace events for characters before cursor.
         */
        var eq = I.TryGet<EventQueue>();
        if (eq != null)
        {
            for (int i = 0; i < beforeLength; i++)
            {
                eq.Push(new Event(Event.INPUT_KEY_PRESSED, "(backspace)"));
                eq.Push(new Event(Event.INPUT_KEY_RELEASED, "(backspace)"));
            }

            for (int i = 0; i < afterLength; i++)
            {
                eq.Push(new Event(Event.INPUT_KEY_PRESSED, "(delete)"));
                eq.Push(new Event(Event.INPUT_KEY_RELEASED, "(delete)"));
            }
        }

        return true;
    }
}
