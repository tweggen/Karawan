using System.Numerics;

namespace engine.news;

public class Event
{
    public const string INPUT_KEY_PRESSED = "input.key.pressed";
    public const string INPUT_KEY_RELEASED = "input.key.released";
    public const string INPUT_TOUCH_PRESSED = "input.touch.pressed";
    public const string INPUT_TOUCH_RELEASED = "input.touch.released";
    public const string INPUT_MOUSE_PRESSED = "input.mouse.pressed";
    public const string INPUT_MOUSE_RELEASED = "input.mouse.released";
    public const string INPUT_MOUSE_MOVED = "input.mouse.moved";
    public const string INPUT_MOUSE_WHEEL = "input.mouse.wheel";

    public const string VIEW_SIZE_CHANGED = "view.size.changed";
    public const string MAP_RANGE_EVENT = "map.range";

    public const string RENDER_STATS = "render.stats";

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
    public Vector2 Size = Vector2.One;

    public Event(string type, string code)
    {
        Type = type;
        Code = code;
    }
}