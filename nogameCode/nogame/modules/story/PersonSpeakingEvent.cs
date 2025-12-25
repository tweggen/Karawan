namespace nogame.modules.story;

public class PersonSpeakingEvent : engine.news.Event
{
    public string Person { get; set; }
    public string Animation { get; set; }
    
    public PersonSpeakingEvent(string type, string code) : base(type, code)
    {
    }
}