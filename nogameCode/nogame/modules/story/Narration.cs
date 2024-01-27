using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using engine;
using engine.draw;
using engine.joyce;
using engine.news;
using Ink.Runtime;
using nogame.modules.osd;
using static engine.Logger;

namespace nogame.modules.story;

public class Narration : AModule, IInputPart
{
    private Story? _currentStory = null;
    private string _currentString = "";

    private DefaultEcs.Entity _eSentence = default;

    public float MY_Z_ORDER { get; set; } = 50f;
    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new MyModule<nogame.modules.osd.Display>(),
    };

    private void _prepareEntity()
    {
        if (_eSentence.IsAlive) return;
        var mDisplay = M<nogame.modules.osd.Display>();
        _eSentence = _engine.CreateEntity($"nogame.modules.story sentence");
        _eSentence.Set(new engine.draw.components.OSDText(
            new Vector2((mDisplay.Width-500f)/2f , 360f),
            new Vector2(500f, 40),
            "",
            16,
            0xffcccccc,
            0x00000000,
            HAlign.Center));
    }


    private void _dismissSentence()
    {
        _prepareEntity();
        _eSentence.Get<engine.draw.components.OSDText>().Text = "";
    }
    

    private void _displayNextSentence()
    {
        _prepareEntity();
        _eSentence.Get<engine.draw.components.OSDText>().Text = _currentString;
    }
    
    
    private void _advanceStory()
    {
        if (_currentStory.canContinue)
        {
            _currentString = _currentStory.Continue();
            _displayNextSentence();
            
            /*
             * Display story item.
             */
            Trace($"story: {_currentString}");
        }
        else
        {
            _dismissSentence();
            _currentStory = null;
        }
#if false
        foreach (var choice in _currentStory.currentChoices)
        {
            Trace($"story.choice: {choice.text}");
        }
#endif
    }
    
    
    private void _triggerNextStory()
    {
        var stream = engine.Assets.Open("story1.json");
        using var sr = new StreamReader(stream, Encoding.UTF8);
        string jsonStory = sr.ReadToEnd();
        _currentStory = new Story(jsonStory);

        _advanceStory();
    }


    private void _onActionKey()
    {
        _advanceStory();
    }
    

    public void InputPartOnInputEvent(engine.news.Event ev)
    {
        if (ev.Type == Event.INPUT_KEY_PRESSED)
        {
            switch (ev.Code)
            {
                case "(action)":
                    if (_currentStory != null)
                    {
                        _onActionKey();
                        ev.IsHandled = true;
                    }

                    break;
                default:
                    break;
            }
        }
    }
    
    
    public override void ModuleDeactivate()
    {
        I.Get<engine.news.InputEventPipeline>().RemoveInputPart(this);
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate(engine.Engine engine0)
    {
        base.ModuleActivate(engine0);
        engine0.AddModule(this);
        I.Get<engine.news.InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);

        _triggerNextStory();
    }
}