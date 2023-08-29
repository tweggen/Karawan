namespace engine;

public class PropertyEvent : engine.news.Event
{
    public object PropertyValue;

    public static readonly string PROPERTY_CHANGED = "property.changed";

    public PropertyEvent(string type, string code, object propertyValue) : base(type, code)
    {
        PropertyValue = propertyValue;
    }
}