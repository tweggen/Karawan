using System.Collections.Generic;
using System.Text;

namespace engine.inputs;


/**
 * What typed text means for a text field: runs of printable characters to insert, and the
 * control characters among them as the edit actions they stand for (KAR-425).
 *
 * Text reaches a field already composed - INPUT_KEY_CHARACTER from SDL's text input,
 * INPUT_TEXT_REPLACE from the Android keyboard (KarawanInputConnection.CommitText) - and
 * nothing filtered it since Platform._onKeyChar went with the Silk input path. A soft
 * keyboard's Enter commonly commits "\n", which a field then stored as a character.
 * Restores what _onKeyChar did:
 *   \b -> Backspace, DEL (0x7f) -> Delete, \n or \r (and \r\n, once) -> Enter, \t -> Tab,
 *   any other control character dropped.
 */
public static class TextInputFilter
{
    public enum Control
    {
        None,
        Backspace,
        Delete,
        Enter,
        Tab
    }


    /** One piece of typed text: printable text to insert, or one control action. */
    public readonly record struct Piece(string Text, Control Control)
    {
        public static Piece Insert(string text) => new(text, Control.None);
        public static Piece Act(Control control) => new(null, control);
    }


    public static IReadOnlyList<Piece> Split(string text)
    {
        var pieces = new List<Piece>();
        if (string.IsNullOrEmpty(text)) return pieces;

        var run = new StringBuilder();
        void Flush()
        {
            if (run.Length == 0) return;
            pieces.Add(Piece.Insert(run.ToString()));
            run.Clear();
        }

        for (int i = 0; i < text.Length; ++i)
        {
            char c = text[i];
            Control control = c switch
            {
                '\b' => Control.Backspace,
                '\x7f' => Control.Delete,
                '\n' => Control.Enter,
                '\r' => Control.Enter,
                '\t' => Control.Tab,
                _ => Control.None
            };

            if (control != Control.None)
            {
                Flush();
                pieces.Add(Piece.Act(control));
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') ++i;
            }
            else if (!char.IsControl(c))
            {
                run.Append(c);
            }
        }

        Flush();
        return pieces;
    }
}
