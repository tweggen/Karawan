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

    private builtin.jt.Factory _factory = I.Get<builtin.jt.Factory>();
    private builtin.jt.Parser _parser = null;


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
            _parser.RootWidget.PropagateInputEvent(ev);
        }
    }
    

    public override void ModuleDeactivate()
    {
        I.Get<InputEventPipeline>().RemoveInputPart(this);

        var children = _parser.RootWidget.Children;
        if (children != null)
        {
            foreach (var wChild in children)
            {
                wChild.Parent = null;
                wChild.Dispose();
            }
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
            /*
             * Read the xml
             */
            XmlDocument xDoc = new XmlDocument();
            xDoc.Load(engine.Assets.Open("menu.xml"));
            
            /*
             * Parse the document
             */
            _parser = new Parser(xDoc, _factory);
            
            /*
             * Open the default menu.
             */
            var wMenu = _parser.Build("menuOptions");
            if (null != wMenu)
            {
                _parser.RootWidget.AddChild(wMenu);
            }
        }
        catch (Exception e)
        {
            Error($"Exception opening menu: {e}");
        }

        I.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
    }
}
