namespace builtin.jt;

public class WidgetEvent : engine.news.Event
{
    public Widget Widget;
    public System.Numerics.Vector2 RelativePosition;
    
    public WidgetEvent(string type, Widget widget)
        : base(type, null)
    {
        Widget = widget;
    }
}