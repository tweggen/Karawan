using System;
using engine;
using engine.news;

namespace nogame.scenes.root;

public class KeyHandler : engine.IPart
{
    private object _lo = new();
    private Engine _engine;
    private IScene _scene;

#if false
    private void _activateMenu(ref bool isMenuShown, IPart part)
    {
        bool wasShown;
        lock (_lo)
        {
            wasShown = isMenuShown;
            isMenuShown = !isMenuShown;
        }

        if (wasShown)
        {
            part.PartDeactivate();
        }
        else
        {
            part.PartActivate(_engine, _scene);            
        }
    }
#endif    
    
    public void PartOnKeyEvent(KeyEvent keyEvent)
    {
        if (keyEvent.Type != "pressed")
        {
            return;
        }

        switch (keyEvent.Code)
        {
            case "(tab)":
                keyEvent.IsHandled = true;
                (_scene as nogame.scenes.root.Scene).ToggleMap();
                break;
            case "(escape)":
                keyEvent.IsHandled = true;
                (_scene as nogame.scenes.root.Scene).TogglePauseMenu();
                break;
            default:
                break;
        }
    }

    
    public void PartDeactivate()
    {
        _engine.RemovePart(this);
        lock (_lo)
        {
            _engine = null;
            _scene = null;
        }
    }

    
    public void PartActivate(in Engine engine0, in IScene scene0)
    {
        lock (_lo)
        {
            _engine = engine0;
            _scene = scene0;
        }
        _engine.AddPart(20, _scene, this);
    }

    public KeyHandler()
    {
        // TXWTODO: Why do these parts belong to the KeyHandler part?
    }
}