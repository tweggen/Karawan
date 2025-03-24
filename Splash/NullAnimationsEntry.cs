using engine.joyce;

namespace Splash;

public class NullAnimationsEntry : AAnimationsEntry
{
    private bool _isUploaded = false;
    
    public NullAnimationsEntry(in Model? m) : base(in m)
    {
    }


    public override void Upload()
    {
        _isUploaded = true;
    }
    
    public override bool IsUploaded()
    {
        return _isUploaded;
    }

    public override void Dispose()
    {
        _isUploaded = false;
    }

    private static NullAnimationsEntry instance = new(null);
    public static NullAnimationsEntry Instance()
    {
        return instance;
    }
}