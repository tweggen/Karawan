namespace engine.news;

public class KeyEvent
{
    public string Type;
    public string Code;
    
    private bool _isHandled = false;
    public bool IsHandled
    {
        get => _isHandled;
        set
        {
            _isHandled = value;
        }
    }
    
    public KeyEvent(string type, string code)
    {
        Type = type;
        Code = code;
    }
}