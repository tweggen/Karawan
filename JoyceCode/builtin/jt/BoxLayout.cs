using System;
using engine;
using static engine.Logger;


namespace builtin.jt;


internal class BoxLayoutItem : ALayoutItem
{
    public float Flex { get; set; }
}


public class BoxLayout : ALayout
{
    public bool IsHorizontal { get; set; } = false;
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
        
        /*
         * First figure out the proper attribute names
         */
        
        string strOrthoExtent;
        string strOrthoMinExtent;
        string strAxisExtent;
        string strAxisMinExtent;
        string strAxisPos; 
        string strOrthoPos;
        if (IsHorizontal)
        {
            strOrthoExtent = "height";
            strOrthoMinExtent = "minHeight";
            strAxisExtent = "width";
            strAxisMinExtent = "minWidth";
            strOrthoPos = "y";
            strAxisPos = "x";
        }
        else
        {
            strOrthoExtent = "width";
            strOrthoMinExtent = "minWidth";
            strAxisExtent = "height";
            strAxisMinExtent = "minHeight";
            strOrthoPos = "x";
            strAxisPos = "y";
        }

        float maxExtent;
        
        /*
         * Then look, how to layout. If we, i.e. the layout owner, has a width/height
         * (ortho extent)
         */
        if (Parent.HasAttr(strOrthoExtent))
        {
            /*
             * The parent has a specified width, so just apply that to the children.
             */
            maxExtent = Parent.GetAttr(strOrthoExtent, 200f);
        }
        else
        {
            /*
             * The parent has no specified width, so collect the chilren's sizes.
             */

            /*
             * First collect the extent in the non-axis direction,
             * i.e. width on a vbox.
             */
            maxExtent = 0f;
            foreach (var item in items)
            {
                float orthoExtent; 
                if (item.Widget.HasAttr(strOrthoExtent))
                {
                    orthoExtent = item.Widget.GetAttr(strOrthoExtent, 0f);
                }
                else
                {
                    var (width, height) = item.Widget.SizeHint(); 
                    if (IsHorizontal)
                    {
                        orthoExtent = height;
                    }
                    else
                    {
                        orthoExtent = width;
                    }
                }

                float orthoMinExtent = item.Widget.GetAttr(strOrthoMinExtent, 0f);
                maxExtent = Single.Max(maxExtent, Single.Max(orthoExtent, orthoMinExtent));
            }
        }

        /*
         * The position in the axis to layout, e.g. Y in a vbox.
         */
        float currAxisPos = Parent.GetAttr(strAxisPos, 0f);
        float currOrthoPos = Parent.GetAttr(strOrthoPos, 0f);
        foreach (var item in items)
        {
            Widget w = item.Widget;

            float axisMinExtent = w.GetAttr(strAxisMinExtent, 0f);
            float axisExtent;
            if (item.Widget.HasAttr(strAxisExtent))
            {
                axisExtent = item.Widget.GetAttr(strAxisExtent, 0f);
            }
            else
            {
                var (width, height) = item.Widget.SizeHint();
                if (IsHorizontal)
                {
                    axisExtent = width;
                }
                else
                {
                    axisExtent = height;
                }
            }
            float axisEffectiveExtent = Single.Max(axisMinExtent, axisExtent);
            float nextAxisPos = currAxisPos + axisEffectiveExtent;
            
            w[strAxisPos] = currAxisPos;
            w[strAxisExtent] = nextAxisPos - currAxisPos;
            w[strOrthoPos] = currOrthoPos;
            w[strOrthoExtent] = maxExtent;

            currAxisPos = nextAxisPos;
        } 
    }
    
    
    public override (float Width, float Height) SizeHint()
    {
        float totalWidth = 0f;
        float totalHeight = 0f;

        var items = _immutableItems();
        if (null == items || items.Count == 0)
        {
            return (0f, 0f);
        }
        
        foreach (var item in items)
        {
            Widget w = item.Widget;
            var (hintWidth, hintHeight) = (0f, 0f);

            bool hasHeight = w.HasAttr("Height");
            bool hasWidth = w.HasAttr("Width");
            
            float widgetWidth, widgetHeight;
            float effectiveWidth, effectiveHeight;
            
            if (!hasHeight || !hasWidth)
            {
                (hintWidth, hintHeight) = w.SizeHint();
            }

            if (hasWidth)
            {
                widgetWidth = w.GetAttr("width", 0f);
            }
            else
            {
                widgetWidth = hintWidth;
            }
            
            if (hasHeight)
            {
                widgetHeight = w.GetAttr("height", 0f);
            }
            else
            {
                widgetHeight = hintHeight;
            }

            effectiveWidth = Single.Max(w.GetAttr("minWidth", 0f), widgetWidth);
            effectiveHeight = Single.Max(w.GetAttr("minHeight", 0f), widgetHeight);
            
            if (IsHorizontal)
            {
                totalWidth += effectiveWidth;
                totalHeight = Single.Max(totalHeight, effectiveHeight);
            }
            else
            {
                totalWidth = Single.Max(totalWidth, effectiveWidth);
                totalHeight += effectiveHeight;
            }
        }

        return (totalWidth, totalHeight);
    }
}