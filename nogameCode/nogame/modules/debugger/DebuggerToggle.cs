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
        new MyModule<joyce.ui.Module>("nogame.CreateUI") { ShallActivate = false },
        new SharedModule<InputEventPipeline>()
    };

    /**
     * Where the 3D view starts once the debug UI is shown.
     *
     * Read from joyce.ui.Main rather than held here, so the drag handle on the pane's edge
     * and this toggle cannot disagree. Both used to hardcode 500 independently - change one
     * and the panel overlaps the viewport, or leaves a gap, with nothing to say why.
     */
    public Vector2 TopLeft => new(
        joyce.ui.Main.LeftPaneWidth,
        joyce.ui.Main.MenuBarHeight);

    
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
            DeactivateMyModule<joyce.ui.Module>();
            _engine.DisableMouse();
        }
        else
        {
            _engine.SetViewRectangle(TopLeft, Vector2.Zero );
            _engine.EnableMouse();
            ActivateMyModule<joyce.ui.Module>();
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