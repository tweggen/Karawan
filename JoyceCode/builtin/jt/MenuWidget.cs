    namespace builtin.jt;

    public class MenuWidget : Widget
    {
        protected override void _onPropertyChanged(string key, object oldValue, object newValue)
        {
            if (key == "focussed")
            {
                if (newValue == "true" || newValue is bool && (bool)newValue)
                {
                    /*
                    * As soon we receive focus, we forward it to our currently
                    * selected child.
                    */
                    Widget? wFocussableChild = FindOffsetFocussableChild(this, OffsetOrientation.DontCare, 1);
                    if (wFocussableChild != null)
                    {
                        Root?.SetFocussedChild(wFocussableChild);
                    }
                }
            }
        }
    }