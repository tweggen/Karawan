namespace builtin.jt;

public class LuaWidgetContext
{
    internal Widget _widget;

    public object this[string key]
    {
        get
        {
            return _widget[key];
        }
        set
        {
            switch (key)
            {
                case "parser":
                    break;
                default:
                    _widget[key] = value;
                    break;
            }
        }
    }
    
    public LuaWidgetContext(Widget widget)
    {
        _widget = widget;
    }
}
