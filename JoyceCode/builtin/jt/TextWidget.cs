namespace builtin.jt;

public class TextWidget : Widget
{
    public override (float Width, float Height) SizeHint()
    {
        string strText = GetAttr("text", "");
        if (null == strText)
        {
            int a = 1;
        }
        return (strText.Length*10f, 20f);
    }


    public override float HeightForWidth(float w)
    {
        return 20f;
    }
}