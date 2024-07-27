using System;
using engine.news;

namespace builtin.jt;

public class InputWidget : TextWidget
{
    public string ValueProperty = "text";

    internal int _cursorPos()
    {
        int oldPosition = GetAttr("cursorPos", -1);
        
        if (oldPosition < 0)
        {
            string oldValue = GetAttr(ValueProperty, "");
            int len = oldValue.Length;
            oldPosition = len;
        }

        return oldPosition;
    }

    protected void _moveCursorRelative(int offset)
    {
        string oldValue = GetAttr(ValueProperty, "");
        int oldPosition = GetAttr("cursorPos", -1);
        
        int len = oldValue.Length;
        if (oldPosition < 0)
        {
            oldPosition = len;
        }

        int newPos = Int32.Clamp(offset + oldPosition, 0, len);

        this["cursorPos"] = newPos;
    }


    protected void _delete()
    {
        string oldValue = GetAttr(ValueProperty, "");
        int oldPosition = GetAttr("cursorPos", -1);
        
        int len = oldValue.Length;
        if (oldPosition < 0)
        {
            oldPosition = len;
        }

        if (oldPosition == len)
        {
            return;
        }

        string newValue =
            oldValue.Substring(0, oldPosition)
            + oldValue.Substring(oldPosition + 1);

        this[ValueProperty] = newValue;
    }


    protected void _backspace()
    {
        string oldValue = GetAttr(ValueProperty, "");
        int oldPosition = GetAttr("cursorPos", -1);
        
        int len = oldValue.Length;
        if (oldPosition < 0)
        {
            oldPosition = len;
        }

        if (oldPosition == 0)
        {
            return;
        }

        string newValue =
            oldValue.Substring(0, oldPosition-1)
            + oldValue.Substring(oldPosition);

        this["cursorPos"] = oldPosition - 1;
        this[ValueProperty] = newValue;
    }


    protected void _insert(string newString)
    {
        string oldValue = GetAttr(ValueProperty, "");
        int oldPosition = GetAttr("cursorPos", -1);
        
        int len = oldValue.Length;
        if (oldPosition < 0)
        {
            oldPosition = len;
        }

        string newValue =
            oldValue.Substring(0, oldPosition)
            + newString
            + oldValue.Substring(oldPosition);

        this[ValueProperty] = newValue;
        this["cursorPos"] = oldPosition + newString.Length;
    }
    
    
    protected override void _handleSelfInputEvent(engine.news.Event ev)
    {
        bool isFocussed = this.IsFocussed;
        if (!isFocussed)
        {
            //base._handleSelfInputEvent(ev);
            //return;
        }
        else
        {
            if (ev.Type.StartsWith(Event.INPUT_KEY_CHARACTER)) 
            {
                /*
                 * Insert input at current cursor.
                 */
                _insert(ev.Code);
                ev.IsHandled = true;
        
                _emitEvent("onChange");

            }
            else if (ev.Type.StartsWith(Event.INPUT_KEY_PRESSED))
            {
                /*
                 * Modify input according to control character
                 */
                switch (ev.Code)
                {
                    case " ":
                    case "w":
                    case "s":
                    case "a":
                    case "d":
                    case "e":
                        /*
                         * Capture the usual selection keys, because we are inputting.
                         */
                        ev.IsHandled = true;
                        break;
                    case "(cursorright)":
                        _moveCursorRelative(1);
                        ev.IsHandled = true;
                        break;
                    case "(cursorleft)":
                        _moveCursorRelative(-1);
                        ev.IsHandled = true;
                        break;
                    case "(delete)":
                        _delete();
                        ev.IsHandled = true;
                        break;
                    case "(backspace)":
                        _backspace();
                        ev.IsHandled = true;
                        break;
                    case "(enter)":
                        // TXWTODO: Accept, leave focus
                        break;
                    case "(escape)":
                        // TXWTODO: Leave focus?
                        break;
                    default:
                        break;
                }
            }
        }

        if (!ev.IsHandled)
        {
            base._handleSelfInputEvent(ev);
        }
    }


}