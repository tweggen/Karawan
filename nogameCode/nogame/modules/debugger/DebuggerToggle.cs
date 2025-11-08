using System.Collections.Generic;
using System.Numerics;
using engine;
using engine.news;

namespace nogame.modules.debugger;

public class DebuggerToggle : AModule, IInputPart
{
    public float MY_Z_ORDER { get; set; } = 25f;

    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new MyModule<modules.debugger.Module>("nogame.CreateUI") { ShallActivate = false },
        new SharedModule<InputEventPipeline>()
    };

    public Vector2 TopLeft { get; set; } = new(500f, 20f);
    
    private bool _isUIShown = false;


    private void _toggleDebugger()
    {
        bool isUIShown;
        lock (_lo)
        {
            isUIShown = _isUIShown;
            _isUIShown = !isUIShown;
        }

        if (isUIShown)
        {
            _engine.SetViewRectangle(Vector2.Zero, Vector2.Zero );
            DeactivateMyModule<modules.debugger.Module>();
            _engine.DisableMouse();
        }
        else
        {
            _engine.SetViewRectangle(TopLeft, Vector2.Zero );
            _engine.EnableMouse();
            ActivateMyModule<modules.debugger.Module>();
        }
    }
    
    
    public void InputPartOnInputEvent(Event ev)
    {
        switch (ev.Type)
        {
            case Event.INPUT_KEY_PRESSED:
                switch (ev.Code)
                {
                    case "(F12)":
                        ev.IsHandled = true;
                        _toggleDebugger();
                        break;
                    default:
                        break;
                }
                break;
        }
    }

    
    protected override void OnModuleDeactivate()
    {
        M<InputEventPipeline>().RemoveInputPart(this);
    }
    
    
    protected override void OnModuleActivate()
    {
        M<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
    }

}