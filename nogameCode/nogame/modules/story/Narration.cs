using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using engine;
using engine.draw;
using engine.news;
using Ink.Runtime;
using nogame.modules.osd;
using ObjLoader.Loader.Common;
using static engine.Logger;

namespace nogame.modules.story;


public class Narration : AModule, IInputPart
{
    private Story? _currentStory = null;
    private string _currentString = "";
    private int _currentNChoices = 0;

    private Boom.ISound _soundTty = null;
    
    private DefaultEcs.Entity _eSentence = default;

    public float MY_Z_ORDER { get; set; } = 24.5f;
    
    
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
            HAlign.Center,
            VAlign.Bottom));
        _eSentence.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new Event("nogame.modules.story.sentence.onClick", null)
        });
    }


    private void _dismissSentence()
    {
        _prepareEntity();
        _eSentence.Get<engine.draw.components.OSDText>().Text = "";
    }
    

    private void _displayNextSentence()
    {
        _prepareEntity();

        
        string strDisplay = "";

        lock (_lo)
        {
            strDisplay = _currentString;

            int index = 1;
            _currentNChoices = 0;
            if (_currentStory.currentChoices.Count > 0)
            {
                int nChoices = _currentStory.currentChoices.Count;
                for (int i = 0; i < nChoices; ++i)
                {
                    ++_currentNChoices;
                    strDisplay += $"\n{i+1}: {_currentStory.currentChoices[i].text}";
                }
            }
        }

        _eSentence.Get<engine.draw.components.OSDText>().Text = strDisplay;

        _soundTty.Stop();
        _soundTty.Volume = 0.02f;
        _soundTty.Play();
    }
    
    
    private void _advanceStory()
    {
        bool dismiss = false;
        lock (_lo)
        {
            if (null == _currentStory)
            {
                return;
            }
            
            if (!_currentStory.canContinue)
            {
                dismiss = true;
                _currentStory = null;
            }
        }
        if (dismiss)
        {
            _dismissSentence();
            return;
        }

        lock (_lo)
        {
            /*
             * If we have a current string, display it.
             */
            _currentString = _currentStory.Continue();
            /*
             * But also, if there are choices, display them.
             */
#if false
            if (_currentStory.currentChoices.Count > 0)
            {
                
            }
#endif
        }
            
        _displayNextSentence();
    }
    
    
    private void _triggerNextStory()
    {
        var stream = engine.Assets.Open("story1.json");
        using var sr = new StreamReader(stream, Encoding.UTF8);
        string jsonStory = sr.ReadToEnd();
        lock (_lo)
        {
            _currentStory = new Story(jsonStory);
            _currentStory.BindExternalFunction ("triggerQuest", (string questName) => {
                Trace($"Trigger quest {questName}");
            });
        }

        _advanceStory();
    }


    private void _onActionKey()
    {
        _advanceStory();
    }


    private void _onClickSentence(engine.news.Event ev)
    {
        _advanceStory();
    }
    

    public void InputPartOnInputEvent(engine.news.Event ev)
    {
        int nChoices = 0;
        bool haveStory = false;
        lock (_lo)
        {
            if (_currentStory != null)
            {
                if (!_currentString.IsNullOrEmpty())
                {
                    haveStory = true;
                }

                nChoices = _currentStory.currentChoices.Count;
            }
        }
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
                    if (ev.Code.CompareTo("0") >= 0
                        && ev.Code.CompareTo("9") <= 0)
                    {
                        int number = ev.Code[0] - '0';
                        if (0 == number) number = 10;                            
                        if (nChoices > 0)
                        {
                            if (number <= nChoices)
                            {
                                ev.IsHandled = true;
                                lock (_lo)
                                {
                                    _currentStory.ChooseChoiceIndex(number-1);
                                }
                                _advanceStory();
                            }
                        }
                    }
                    break;
            }
        }
    }
    
    
    public override void ModuleDeactivate()
    {
        I.Get<engine.news.SubscriptionManager>().Unsubscribe("nogame.modules.story.sentence.onClick",_onClickSentence);
        I.Get<engine.news.InputEventPipeline>().RemoveInputPart(this);
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate(engine.Engine engine0)
    {
        base.ModuleActivate(engine0);
        engine0.AddModule(this);

        if (null == _soundTty)
        {
            _soundTty = I.Get<Boom.ISoundAPI>().FindSound("terminal.ogg");
        }
        I.Get<engine.news.InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        I.Get<engine.news.SubscriptionManager>().Subscribe("nogame.modules.story.sentence.onClick",_onClickSentence);
        _triggerNextStory();
    }
}