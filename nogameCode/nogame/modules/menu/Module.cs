using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Xml;
using builtin.jt;
using engine;
using engine.news;
using engine.streets;
using Newtonsoft.Json.Linq;

namespace nogame.modules.menu;

public class Module : AModule, IInputPart
{
    public float MY_Z_ORDER = 1000f;

    private builtin.jt.Widget? _wMenu;
    private builtin.jt.Factory _factory;


    public void InputPartOnInputEvent(Event ev)
    {
        switch (ev.Type)
        {
            case Event.INPUT_KEY_PRESSED:
                switch (ev.Code)
                {
                    case "(escape)":
                        ev.IsHandled = true;
                        ModuleDeactivate();
                        break;
                    default:
                        break;
                }
                break;
            default:
                break;
        }

        if (!ev.IsHandled)
        {
            _wMenu.Root.PropagateInputEvent(ev);
        }
    }
    

    public override void ModuleDeactivate()
    {
        _wMenu.IsVisible = false;
        _wMenu.Dispose();
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate(Engine engine0)
    {
        base.ModuleActivate(engine0);
        _engine.AddModule(this);
        _factory = I.Get<builtin.jt.Factory>();
        
        XmlDocument xDoc = new XmlDocument();
        xDoc.Load(engine.Assets.Open("menu.xml"));
        builtin.jt.Parser jtParser = new Parser(xDoc);
        _wMenu = jtParser.Build(_factory);
        _wMenu.Root = _factory.FindRootWidget();
    }
}