using System.Numerics;

namespace engine.news;

public class Event
{
    public const string INPUT_KEY_PRESSED = "input.key.pressed";
    public const string INPUT_KEY_RELEASED = "input.key.released";
    public const string INPUT_TOUCH_ANY = "input.touch.";
    public const string INPUT_TOUCH_PRESSED = "input.touch.pressed";
    public const string INPUT_TOUCH_RELEASED = "input.touch.released";
    public const string INPUT_FINGER_PRESSED = "input.finger.pressed";
    public const string INPUT_FINGER_RELEASED = "input.finger.released";
    public const string INPUT_FINGER_MOVED = "input.finger.moved";
    public const string INPUT_MOUSE_ANY = "input.mouse.";
    public const string INPUT_MOUSE_PRESSED = "input.mouse.pressed";
    public const string INPUT_MOUSE_RELEASED = "input.mouse.released";
    public const string INPUT_MOUSE_MOVED = "input.mouse.moved";
    public const string INPUT_MOUSE_WHEEL = "input.mouse.wheel";
    public const string INPUT_GAMEPAD_TRIGGER_MOVED = "input.gamepad.trigger.moved";
    public const string INPUT_GAMEPAD_STICK_MOVED = "input.gamepad.stick.moved";
    public const string INPUT_GAMEPAD_BUTTON_PRESSED = "input.gamepad.button.pressed";
    public const string INPUT_GAMEPAD_BUTTON_RELEASED = "input.gamepad.button.released";

    public const string VIEW_SIZE_CHANGED = "view.size.changed";
    public const string MAP_RANGE_EVENT = "map.range";

    public const string RENDER_STATS = "render.stats";

    /**
     * Behavior events can
     */
    public const string BEHAVIOR_LOST_CUSTOM_EVENT = "behavior.lost.custom.";

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
    public uint Data1;
    public uint Data2;
    public uint Data3; 

    public Event(string type, string code)
    {
        Type = type;
        Code = code;
    }
}