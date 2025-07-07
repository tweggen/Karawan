using System.Numerics;

namespace engine.news;

public class Event
{
    public const string INPUT_KEY_PRESSED = "input.key.pressed";
    public const string INPUT_KEY_RELEASED = "input.key.released";
    public const string INPUT_KEY_CHARACTER = "input.key.character";
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

    public const string INPUT_LOGICAL_PRESSED = "input.logical.pressed";
    public const string INPUT_LOGICAL_MOVED = "input.logical.moved";
    public const string INPUT_LOGICAL_RELEASED = "input.logical.released";
    
    public const string INPUT_BUTTON_PRESSED = "input.button.pressed";
    public const string INPUT_BUTTON_RELEASED = "input.button.released";


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

    public bool IsReleased
    {
        get => Type.EndsWith(".released");
    }

    public bool IsPressed
    {
        get => Type.EndsWith(".pressed");
    }
    
    public string Type;
    public string Code;
    
    /**
     * The position in original dimenstions.
     * This may coincide with the logical position but does not have to.
     */
    public Vector2 PhysicalPosition = Vector2.Zero;
    
    /**
     * The size in original dimensions.
     */
    public Vector2 PhysicalSize = Vector2.One;
    
    /**
     * The position scaled to the 0...1 range.
     * Note, that other positions are valid.
     */
    public Vector2 LogicalPosition = Vector2.Zero;
    
    
    public uint Data1;
    public uint Data2;
    public uint Data3;


    public string ToKey() => $"{Type}:{Code}";

    public override string ToString()
    {
        return
            $"{{ \"type\": \"{Type}\", \"code\": \"{Code}\", \"data1\": {Data1}, \"data2\": {Data2}, \"data3\": \"{Data3}\"}}";
    }
    
    
    public Event(string type, string code)
    {
        Type = type;
        Code = code;
    }
}