using System.Numerics;

namespace engine.news;

public class Event
{
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