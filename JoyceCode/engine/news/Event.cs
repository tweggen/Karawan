using System.Numerics;

namespace engine.news;

public class Event
{
    public static readonly string INPUT_KEY_PRESSED = "input.key.pressed";
    public static readonly string INPUT_KEY_RELEASED = "input.key.released";
    public static readonly string INPUT_TOUCH_PRESSED = "input.touch.pressed";
    public static readonly string INPUT_TOUCH_RELEASED = "input.touch.released";
    public static readonly string INPUT_MOUSE_PRESSED = "input.mouse.pressed";
    public static readonly string INPUT_MOUSE_RELEASED = "input.mouse.released";
    public static readonly string INPUT_MOUSE_MOVED = "input.mouse.moved";
    public static readonly string INPUT_MOUSE_WHEEL = "input.mouse.wheel";

    public static readonly string VIEW_SIZE_CHANGED = "view.size.changed";
    
    private bool _isHandled = false;
    public bool IsHandled
    {
        get => _isHandled;
        set
        {
            _isHandled = value;
        }
    }
    
    public string Type;
    public string Code;
    public Vector2 Position = Vector2.Zero;

    public Event(string type, string code)
    {
        Type = type;
        Code = code;
    }
}