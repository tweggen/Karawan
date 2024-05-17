namespace builtin.jt;

public class TextWidget : Widget
{
    public override (float, float) SizeHint()
    {
        return (0f, 0f);
    }


    public override (float, float) MinSize()
    {
        var (width, height) = base.MinSize();
        return (width, height);
    }


    public override float HeightForWidth(float w)
    {
        return 0f;
    }
}