namespace builtin.jt;

/**
 * Top level widget that associates a widget with a factory.
 */
public class RootWidget : Widget
{
    private Factory _factory; 
    public required Factory Factory
    {
        set
        {
            lock (_lo)
            {
                _factory = value;
            }
        }
        get
        {
            lock (_lo)
            {
                return _factory;
            }
        }
    }


    public override void RemoveChild(Widget child)
    {
        base.RemoveChild(child);
        child.Root = null;
    }
    

    public override void AddChild(Widget child)
    {
        child.Root = this;
        base.AddChild(child);
    }
}
