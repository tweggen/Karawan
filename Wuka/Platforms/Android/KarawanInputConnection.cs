using Android.Text;
using Android.Views;
using Android.Views.InputMethods;
using engine;
using engine.news;
using Java.Lang;

namespace Wuka;

/**
 * Custom InputConnection that wraps SDL's InputConnection to properly handle
 * Android IME composition. Instead of letting SDL translate IME operations into
 * backspace + character sequences (which corrupts our InputWidget's cursor position),
 * we intercept commitText/setComposingText and emit INPUT_TEXT_REPLACE events.
 */
public class KarawanInputConnection : Java.Lang.Object, IInputConnection
{
    private IInputConnection _sdlConnection;
    private int _composingLength = 0;


    public KarawanInputConnection(IInputConnection sdlConnection)
    {
        _sdlConnection = sdlConnection;
    }


    public bool CommitText(ICharSequence text, int newCursorPosition)
    {
        var eq = I.Get<EventQueue>();
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


    public bool SetComposingText(ICharSequence text, int newCursorPosition)
    {
        /*
         * No composition preview — just track the composing length.
         * The text will be shown after commit.
         */
        _composingLength = text?.Length() ?? 0;
        return true;
    }


    public bool FinishComposingText()
    {
        _composingLength = 0;
        return true;
    }


    public bool SetComposingRegion(int start, int end)
    {
        _composingLength = System.Math.Max(0, end - start);
        return true;
    }


    public bool DeleteSurroundingText(int beforeLength, int afterLength)
    {
        /*
         * This is a real text deletion (not composing region management).
         * Emit backspace events for characters before cursor.
         */
        var eq = I.Get<EventQueue>();
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


    public bool SendKeyEvent(KeyEvent e)
    {
        /*
         * Forward physical key events to SDL's connection which will
         * translate them into Silk.NET keyboard events.
         */
        return _sdlConnection.SendKeyEvent(e);
    }


    /*
     * Delegate remaining IInputConnection methods to SDL's connection.
     */
    public bool BeginBatchEdit() => _sdlConnection.BeginBatchEdit();
    public bool EndBatchEdit() => _sdlConnection.EndBatchEdit();
    public bool ClearMetaKeyStates(MetaKeyStates states) => _sdlConnection.ClearMetaKeyStates(states);
    public bool CommitCompletion(CompletionInfo text) => _sdlConnection.CommitCompletion(text);
    public bool CommitCorrection(CorrectionInfo correctionInfo) => _sdlConnection.CommitCorrection(correctionInfo);
    public ICharSequence GetSelectedTextFormatted(GetTextFlags flags) => _sdlConnection.GetSelectedTextFormatted(flags);
    public ICharSequence GetTextAfterCursorFormatted(int length, GetTextFlags flags) => _sdlConnection.GetTextAfterCursorFormatted(length, flags);
    public ICharSequence GetTextBeforeCursorFormatted(int length, GetTextFlags flags) => _sdlConnection.GetTextBeforeCursorFormatted(length, flags);
    public ExtractedText GetExtractedText(ExtractedTextRequest request, GetTextFlags flags) => _sdlConnection.GetExtractedText(request, flags);
    public CapitalizationMode GetCursorCapsMode(CapitalizationMode reqModes) => _sdlConnection.GetCursorCapsMode(reqModes);
    public bool PerformEditorAction(ImeAction actionCode) => _sdlConnection.PerformEditorAction(actionCode);
    public bool PerformContextMenuAction(int id) => _sdlConnection.PerformContextMenuAction(id);
    public bool PerformPrivateCommand(string action, Android.OS.Bundle data) => _sdlConnection.PerformPrivateCommand(action, data);
    public bool SetSelection(int start, int end) => _sdlConnection.SetSelection(start, end);
    public bool ReportFullscreenMode(bool enabled) => _sdlConnection.ReportFullscreenMode(enabled);
    public bool RequestCursorUpdates(int cursorUpdateMode) => _sdlConnection.RequestCursorUpdates(cursorUpdateMode);
    public Android.OS.Handler Handler => _sdlConnection.Handler;
    public void CloseConnection() => _sdlConnection.CloseConnection();
    public bool CommitContent(InputContentInfo inputContentInfo, InputContentFlags flags, Android.OS.Bundle opts) => _sdlConnection.CommitContent(inputContentInfo, flags, opts);
    public bool DeleteSurroundingTextInCodePoints(int beforeLength, int afterLength) => _sdlConnection.DeleteSurroundingTextInCodePoints(beforeLength, afterLength);
}
