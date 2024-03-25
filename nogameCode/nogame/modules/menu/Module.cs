using System;
using System.Xml;
using builtin.jt;
using engine;
using engine.news;
using static engine.Logger;

namespace nogame.modules.menu;


public class Module : AModule, IInputPart
{
    public float MY_Z_ORDER = 1000f;

    private builtin.jt.Widget? _wMenu;
    private builtin.jt.Factory _factory = I.Get<builtin.jt.Factory>();


    public void InputPartOnInputEvent(Event ev)
    {
        bool doDeactivate = false;
        switch (ev.Type)
        {
            case Event.INPUT_KEY_PRESSED:
                switch (ev.Code)
                {
                    case "(escape)":
                        doDeactivate = true;
                        break;
                }
                break;
            
            case Event.INPUT_GAMEPAD_BUTTON_PRESSED:
                switch (ev.Code)
                {
                    case "Back":
                        doDeactivate = true;
                        break;
                }

                break;
        }


        if (doDeactivate)
        {
            ev.IsHandled = true;
            ModuleDeactivate();
        }

        if (!ev.IsHandled)
        {
            if (null != _wMenu)
            {
                _wMenu.Root.PropagateInputEvent(ev);
            }
        }
    }
    

    public override void ModuleDeactivate()
    {
        I.Get<InputEventPipeline>().RemoveInputPart(this);

        if (null != _wMenu)
        {
            _factory.FindRootWidget().RemoveChild(_wMenu);
            _wMenu.Dispose();
            _wMenu = null;
        }

        _engine.GamePlayState = GamePlayStates.Running;
        
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);

        _engine.GamePlayState = GamePlayStates.Paused;

        try
        {
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(engine.Assets.Open("menu.xml"));
            builtin.jt.Parser jtParser = new Parser(xDoc);
            _wMenu = jtParser.Build(_factory, "menuOptions");
            if (null != _wMenu)
            {
                _factory.FindRootWidget().AddChild(_wMenu);
            }
        }
        catch (Exception e)
        {
            Error($"Exception opening menu: {e}");
        }

        I.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
    }
}
