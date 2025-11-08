using engine;

namespace joyce.ui;

public abstract class APart
{
    protected Engine _engine;
    protected Main _uiMain;
    
    
    public abstract void Render(float dt); 
    
    public APart(Main uiMain)
    {
        _uiMain = uiMain;
        _engine = I.Get<Engine>();
    }
}