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

    public Event(string type, string code)
    {
        Type = type;
        Code = code;
    }
}