using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using engine;
using engine.draw;
using engine.geom;
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
    private List<DefaultEcs.Entity> _listEOptions;

    public float MY_Z_ORDER { get; set; } = 24.5f;

    public float BottomY { get; set; } = 400f;
    public float LineHeight { get; set; } = 16f;

    public uint TextColor { get; set; } = 0xffcccccc;
    public uint ChoiceColor { get; set; } = 0xffbbdddd;
    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new MyModule<nogame.modules.osd.Display>(),
    };
    

    private void _prepareSentence()
    {
        if (_eSentence.IsAlive) return;
        var mDisplay = M<nogame.modules.osd.Display>();
        _eSentence = _engine.CreateEntity($"nogame.modules.story sentence");
        _eSentence.Set(new engine.draw.components.OSDText(
            new Vector2((mDisplay.Width-500f)/2f , BottomY-LineHeight),
            new Vector2(500f, 40),
            "",
            16,
            TextColor,
            0x00000000,
            HAlign.Center,
            VAlign.Top));
        _eSentence.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new Event("nogame.modules.story.sentence.onClick", null)
        });
    }


    private DefaultEcs.Entity _createOptionEntity(int idx, float y, string text)
    {
        var mDisplay = M<nogame.modules.osd.Display>();
        DefaultEcs.Entity eOption = _engine.CreateEntity($"nogame.modules.story option {idx}");
        eOption.Set(new engine.draw.components.OSDText(
            new Vector2((mDisplay.Width-500f)/2f , y),
            new Vector2(500f, 16),
            text,
            16,
            ChoiceColor,
            0x00000000,
            HAlign.Center,
            VAlign.Top));
        eOption.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new Event("nogame.modules.story.sentence.onClick", $"{idx+1}")
        });
        return eOption;
    }

    private void _prepareChoices()
    {
        _listEOptions = new();
    }
    
    private void _prepareDisplay()
    {
        _prepareSentence();
    }


    private void _dismissChoices()
    {
        lock (_lo)
        {
            if (null != _listEOptions)
            {
                _engine.AddDoomedEntities(_listEOptions);
                _listEOptions = null;
            }
        }
    }


    private void _dismissSentence()
    {
        if (_eSentence.IsAlive)
        {
            _eSentence.Get<engine.draw.components.OSDText>().Text = "";
        }
    }
    

    private void _dismissDisplay()
    {
        _dismissSentence();
        _dismissChoices();
    }
    

    private int _countLF(string str)
    {
        int count = 0;
        foreach (char c in str)
        {
            if (c == '\n') count++;
        }

        return count;
    }
    

    private void _displayNextSentence()
    {
        _prepareSentence();

        
        string strDisplay = "";
        int nLFs = 0;

        float ytop;
        lock (_lo)
        {
            strDisplay = _currentString;
            nLFs = _countLF(_currentString);

            ytop = BottomY - LineHeight * (nLFs + 1) - 32f;

            int index = 1;
            _currentNChoices = 0;
            if (_currentStory.currentChoices.Count > 0)
            {
                _prepareChoices();
                int nChoices = _currentStory.currentChoices.Count;
                for (int i = 0; i < nChoices; ++i)
                {
                    var eChoice = _createOptionEntity(i,  ytop + LineHeight * (i+1f), $"{i+1}) " + _currentStory.currentChoices[i].text);
                    _listEOptions.Add(eChoice);
                    ++_currentNChoices;
                }
            }
            else
            {
                _dismissChoices();
            }
        }

        ref var cSentenceOSDText = ref _eSentence.Get<engine.draw.components.OSDText>();
        cSentenceOSDText.Text = strDisplay;
        cSentenceOSDText.Position.Y = ytop;

        _soundTty.Stop();
        _soundTty.Volume = 0.02f;
        _soundTty.Play();
    }
    
    
    private void _advanceStory()
    {
        
        /*
         * Look for the next thing coming from the narration engine.
         */
        bool dismissAll = false;
        lock (_lo)
        {
            if (null == _currentStory)
            {
                return;
            }
            
            if (!_currentStory.canContinue)
            {
                dismissAll = true;
                _currentStory = null;
            }
        }
        
        /*
         * If the story won't continue, just remove the display.
         */
        if (dismissAll)
        {
            _dismissDisplay();
            return;
        }

        /*
         * We should display something. So read it from the engine and bring
         * it to the display.
         */
        lock (_lo)
        {
            /*
             * If we have a current string, display it.
             */
            _currentString = _currentStory.Continue();
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
            _currentStory.BindExternalFunction ("triggerQuest", (string questName) =>
            {
                engine.quest.IQuest quest = I.Get<engine.quest.Manager>().Get(questName);
                if (null != quest)
                {
                    quest.ModuleActivate(_engine);
                }
            });
        }

        _advanceStory();
    }


    private void _onActionKey()
    {
        _advanceStory();
    }


    private void _advanceChoice(int number)
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
                if (number > 1 && number <= nChoices)
                {
                    _currentStory.ChooseChoiceIndex(number - 1);
                }
            }
        }
        _advanceStory();
    }

    
    private void _onClickSentence(engine.news.Event ev)
    {
        if (ev.Code != null)
        {
            if (Int32.TryParse(ev.Code, out var number))
            {
                _advanceChoice(number);
            }
        }
        else
        {
            _advanceStory();
        }
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
                                _advanceChoice(number);
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