namespace builtin.modules.story;

public class CurrentStateEvent : engine.news.Event
{
    public bool MayConverse { get; set; }
    public bool ShallBeInteractive { get; set; }
    
    
    public CurrentStateEvent(string type, string code) : base(type, code)
    {
    }
}
