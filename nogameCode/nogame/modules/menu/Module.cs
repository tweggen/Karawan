using System;
using System.Xml;
using builtin.jt;
using engine;
using engine.news;
using static engine.Logger;

namespace nogame.modules.menu;


public class Module : AModule, IInputPart
{
    public float MY_Z_ORDER { get; set; } = 1000f;

    private builtin.jt.ImplementationFactory _implementationFactory = I.Get<builtin.jt.ImplementationFactory>();
    private builtin.jt.Parser _parser = null;


    public void InputPartOnInputEvent(Event ev)
    {
        bool doDeactivate = false;
        /*
         * Let everything with screen locality be handled by the visual
         * system handling clicks. 
         */
        if (ev.Type.StartsWith(Event.INPUT_MOUSE_ANY) || ev.Type.StartsWith(Event.INPUT_TOUCH_ANY))
        {
            return;
        }
        
        switch (ev.Type)
        {
            case Event.INPUT_BUTTON_PRESSED:
                switch (ev.Code)
                {
                    case "<menu1>":
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
            _parser.Layer("pausemenu").PropagateInputEvent(ev);
        }
    }
    

    public override void ModuleDeactivate()
    {
        I.Get<InputEventPipeline>().RemoveInputPart(this);

        var children = _parser.Layer("pausemenu").Children;
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
            _parser = new Parser(xDoc, _implementationFactory);
            
            /*
             * Open the default menu.
             */
            var wMenu = _parser.Build("menuOptions");
            if (null != wMenu)
            {
                _parser.Layer(wMenu["layer"].ToString()).AddChild(wMenu);
                _parser.Layer(wMenu["layer"].ToString()).SetFocussedChild(wMenu.FindFirstFocussableChild());
            }
        }
        catch (Exception e)
        {
            Error($"Exception opening menu: {e}");
        }

        I.Get<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
    }
}
