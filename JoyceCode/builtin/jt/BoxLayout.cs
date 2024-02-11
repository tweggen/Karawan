using System;
using static engine.Logger;


namespace builtin.jt;


internal class BoxLayoutItem : ALayoutItem
{
    public float Flex { get; set; }
}


public class BoxLayout : ALayout
{
    private bool _isHorizonal;
    public void RemoveWidget(Widget w)
    {
        _removeItem(w);
    }

    
    public void AddWidget(Widget w, float flex)
    {
        _addItem(new BoxLayoutItem()
        {
            Widget = w,
            Flex = w.GetAttr("flex", 1.0f)
        });
    }

    
    protected override void _doActivate()
    {
        base._doActivate();
        
        var items = _immutableItems();
        if (null == items) return;
        
        string strOrthoExtent;
        string strAxisExtent;
        string strAxisPos; 
        string strOrthoPos;
        if (_isHorizonal)
        {
            strOrthoExtent = "height";
            strAxisExtent = "width";
            strOrthoPos = "y";
            strAxisPos = "x";
        }
        else
        {
            strOrthoExtent = "width";
            strAxisExtent = "height";
            strOrthoPos = "x";
            strAxisPos = "y";
        }
        
        /*
         * First collect the extent in the non-axis direction,
         * i.e. width on a vbox.
         */
        float maxExtent = 0f;
        foreach (var item in items)
        {
            maxExtent = Single.Max(maxExtent, item.Widget.GetAttr(strOrthoExtent, 0f));
        }
        
        /*
         * The position in the axis to layout, e.g. Y in a vbox.
         */
        float currAxisPos = Parent.GetAttr(strAxisPos, 0f);
        float currOrthoPos = Parent.GetAttr(strOrthoPos, 0f);
        foreach (var item in items)
        {
            Widget w = item.Widget;

            float nextAxisPos = currAxisPos + w.GetAttr(strAxisExtent, 0f);
            
            w[strAxisPos] = currAxisPos;
            w[strAxisExtent] = nextAxisPos - currAxisPos;
            w[strOrthoPos] = currOrthoPos;
            w[strOrthoExtent] = maxExtent;

            currAxisPos = nextAxisPos;
        }
    }
}