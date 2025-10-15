using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using engine;
using engine.draw;
using engine.draw.components;
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
    private int _currentNChoices = 0;
    public int _chosenOption = 0;

    private Boom.ISound _soundTty = null;
    
    private DefaultEcs.Entity _eSentence = default;
    private List<DefaultEcs.Entity> _listEOptions;

    public float MY_Z_ORDER { get; set; } = 24.5f;

    public float BottomY { get; set; } = (400f-20f);
    public float LineHeight { get; set; } = 16f+4f;

    public uint TextColor { get; set; } = 0xffcccccc;
    public uint TextFill { get; set; } = 0x00000000;
    public uint ChoiceColor { get; set; } = 0xffbbdddd;
    public uint ChoiceFill { get; set; } = 0x00000000;

    
    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<AutoSave>(),
        new SharedModule<nogame.modules.osd.Display>(),
        new SharedModule<InputEventPipeline>(),
        new SharedModule<Saver>()
    };
    

    private void _prepareSentence()
    {
        if (_eSentence.IsAlive) return;
        var mDisplay = M<nogame.modules.osd.Display>();
        _eSentence = _engine.CreateEntity($"nogame.modules.story sentence");
        _eSentence.Set(new engine.draw.components.OSDText(
            new Vector2((mDisplay.Width-500f)/2f , BottomY-LineHeight),
            new Vector2(500f, LineHeight),
            "",
            16,
            TextColor,
            TextFill,
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
            new Vector2(500f, LineHeight),
            text,
            16,
            ChoiceColor,
            ChoiceFill,
            HAlign.Center,
            VAlign.Top));
        eOption.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new Event("nogame.modules.story.sentence.onClick", $"{idx+1}")
        });
        return eOption;
    }


    /**
     * Update the markers in a way that the currently chosen option is highlighted.
     */
    private void _updateOptions()
    {
        if (_currentNChoices > 0)
        {
            int idx = 0;
            foreach (var eChoice in _listEOptions)
            {
                if (idx == _chosenOption)
                {
                    eChoice.Get<OSDText>().BorderColor = ChoiceColor; 
                }
                else
                {
                    eChoice.Get<OSDText>().BorderColor = 0;
                }
                idx++;
            }
        }
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
        int length = str.Length;

        while (length > 0)
        {
            if (str[length - 1] == '\n')
            {
                --length;
            }
            else
            {
                break;
            }
        }
        
        for (int i=0; i<length; ++i)
        {
            char c = str[i];
            if (c == '\n') count++;
        }

        return count;
    }
    

    private void _displaySentence()
    {
        _prepareSentence();

        
        string strDisplay = "";
        int nLFs = 0;

        float ytop;
        lock (_lo)
        {
            strDisplay = _currentStory.currentText;
            nLFs = _countLF(_currentStory.currentText);
            
            _currentNChoices = _currentStory.currentChoices.Count;
            int index = 1;
            ytop = BottomY - LineHeight * (nLFs + _currentNChoices + 1);
            if (_currentNChoices > 0)
            {
                _prepareChoices();
                for (int i = 0; i < _currentNChoices; ++i)
                {
                    var eChoice = _createOptionEntity(i,  ytop + LineHeight * (i+1f), $"{i+1}) " + _currentStory.currentChoices[i].text);
                    _listEOptions.Add(eChoice);
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
        cSentenceOSDText.Size.Y = (nLFs+1)*LineHeight;

        _soundTty.Stop();
        _soundTty.Volume = 0.02f;
        _soundTty.Play();
        
        _updateOptions();
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
            _currentStory.Continue();
        }
            
        _displaySentence();
    }


    private void _onActionKey()
    {
        _advanceStory();
    }


    private void _advanceChoice(int number)
    {
        int nChoices = 0;
        lock (_lo)
        {
            if (_currentStory != null)
            {
                if (!_currentStory.currentText.IsNullOrEmpty())
                {
                    nChoices = _currentStory.currentChoices.Count;
                }

                if (number >= 1 && number <= nChoices)
                {
                    _currentStory.ChooseChoiceIndex(number - 1);
                }
            }
        }
        _advanceStory();
    }

    
    private void _onClickSentence(engine.news.Event ev)
    {
        int nChoices;
        lock (_lo)
        {
            if (null == _currentStory)
            {
                return;
            }

            nChoices = _currentNChoices;
        }
        
        if (ev.Code != null)
        {
            if (Int32.TryParse(ev.Code, out var number))
            {
                _advanceChoice(number);
            }
        }
        else
        {
            if (0 == nChoices)
            {
                _advanceStory();
            }
        }
    }
    

    public void InputPartOnInputEvent(engine.news.Event ev)
    {
        int nChoices = 0;
        lock (_lo)
        {
            if (_currentStory == null)
            {
                return;
            }
            if (!_currentStory.currentText.IsNullOrEmpty())
            {
                nChoices = _currentStory.currentChoices.Count;
            }
        }

        bool doSelect = false;
        bool doPrevious = false;
        bool doNext = false;
        int doNth = -1;

        if (ev.Type == Event.INPUT_BUTTON_PRESSED)
        {
            switch (ev.Code)
            {
                case "<interact>":
                    doSelect = true;
                    break;
                case "<cursorup>":
                    doPrevious = true;
                    break;
                case "<cursordown>":
                    doNext = true;
                    break;
            }
        }
        
        if (ev.Type == Event.INPUT_KEY_PRESSED)
        {
            switch (ev.Code)
            {
                case " ":
                case "(enter)":
                case "e":
                    doSelect = true;
                    break;
                case "(cursorup)":
                    doPrevious = true;
                    break;
                case "(cursordown)":
                    doNext = true;
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
                                return;
                            }
                        }
                    }
                    break;
            }
        }

        if (doSelect)
        {
            /*
             * If we are not awaiting options and have a story, we can continue.
             */
            if (0 == nChoices)
            {
                ev.IsHandled = true;
                _onActionKey();
            }
            else
            {
                ev.IsHandled = true;
                _advanceChoice(_chosenOption+1);
            }
        }

        if (_currentNChoices>0 && (doPrevious || doNext))
        {
            ev.IsHandled = true;
            if (doPrevious) _chosenOption = (_chosenOption + _currentNChoices - 1) % _currentNChoices;
            if (doNext) _chosenOption = (_chosenOption + 1) % _currentNChoices;
            _updateOptions();
        }
    }


    private void _saveStory()
    {
        string strStory = ""; 
        lock (_lo)
        {
            if (null != _currentStory)
            {
                if (_currentStory.state != null)
                {
                    strStory = _currentStory.state.ToJson();
                }
            }
        }

        M<AutoSave>().GameState.Story = strStory;
    }
    
    
    public void TriggerPath(string strPath)
    {
        Story currentStory;
        lock (_lo)
        {
            if (null == _currentStory)
            {
                ErrorThrow<InvalidOperationException>($"Requested story {strPath}, but no story had been loaded.");
                return;
            }

            currentStory = _currentStory;
        }

        currentStory.ChoosePathString(strPath, true, null);

        _advanceStory();
    }

    
    private void _ensureStory()
    {
        lock (_lo)
        {
            if (null != _currentStory)
            {
                return;
            }
            _currentStory = null;
            _currentNChoices = 0;
            
            using var stream = engine.Assets.Open("story1.json");
            using var sr = new StreamReader(stream, Encoding.UTF8);
            string jsonStory = sr.ReadToEnd();
            _currentStory = new Story(jsonStory);
            _currentStory.BindExternalFunction("triggerQuest",
                (string questName) => CatchAll(() => I.Get<engine.quest.Manager>().TriggerQuest(questName, true)));
        }
    }
    

    private void _loadStateFromJson(string jsonState)
    {
        lock (_lo)
        {
            _currentStory.state.LoadJson(jsonState);
        }
    }


    private void _onBeforeSaveGame(object sender, object _)
    {
        _saveStory();
    }

    
    private void _onAfterLoadGame(object sender, object objGameState)
    {
        /*
         * We need to have a story to apply a state on.
         */
        _ensureStory();

        /*
         * Now, either we have the state as received from the
         * save game, or we use the default state.
         */
        var gs = objGameState as GameState;
        if (!String.IsNullOrEmpty(gs.Story))
        {
            _loadStateFromJson(gs.Story);
        }
        else
        {
            /*
             * If this story just was created, make the first sentence ready.
             */
            _currentStory.Continue();           
        }
        
        /*
         * After we initialized the desired state, start the story.
         */
        Task.Delay(5000).ContinueWith(t =>
        {
            _engine.QueueMainThreadAction(() =>
            {
                _displaySentence();
            });
        });
    }
    

    protected override void OnModuleDeactivate()
    {
        M<InputEventPipeline>().RemoveInputPart(this);
    }


    protected override void OnModuleActivate()
    {
        if (null == _soundTty)
        {
            _soundTty = I.Get<Boom.ISoundAPI>().FindSound("terminal.ogg");
        }
        M<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);


        M<Saver>().OnBeforeSaveGame += _onBeforeSaveGame;
        M<Saver>().OnAfterLoadGame += _onAfterLoadGame;
        
        Subscribe("nogame.modules.story.sentence.onClick",_onClickSentence);
    }
}