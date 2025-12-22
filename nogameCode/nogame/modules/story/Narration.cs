using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using engine;
using engine.draw;
using engine.draw.components;
using engine.news;
using Ink.Runtime;
using ObjLoader.Loader.Common;
using static engine.Logger;

namespace nogame.modules.story;


/**
 * Implementation of a narrative system for the game.
 *
 * This system is used to drive both narration, prescripted scenes and NPC conversation.
 *
 * Because of its influence on interactivity, a narration system has a state:
 *
 * - idle: Interactive Gameplay, No narration.
 * - conversation: Interactive Gameplay, Narration. User interrupts by leaving
 * - narration: Interactive Gameplay, pre-scripted narration, including meaningful choices.
 * - scripted scene: No interactive Gampleplay, pre-scripted narration, inclufing meaningful choices.
 *
 * - idle (interruptable)
 *   - onNarration: Go.
 *   - mayConverse: Yes
 *   - onScripted: Go
 *
 * - Narration
 *   - mayConverse: No
 *   - mayScripted: Yes
 *
 * - Conversation (interruptable)
 *   Conversation must not have meaningful content, or converts into narration first.
 *   - onNarration: Queue?
 *   - onScripted: Go
 *
 * - Scripted (not interruptible)
 */
public class Narration : AModule, IInputPart
{
    public static readonly string EventTypePersonSpeaking = "nogame.module.story.Narration.PersonSpeaking";
    public static readonly string EventTypeCurrentState = "nogame.module.story.Narration.CurrentState";
    
    private Story? _currentStory = null;
    private string? _currentInstanceId = null;
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


    private bool _mayConverse = true;
    private bool _shallBeInteractive = true;

    public enum State
    {
        Idle,
        Conversation,
        Narration,
        ScriptedScene
    }
    
    private State _currentState = State.Idle;


    private bool _mayTransitionNL(State newState)
    {
        bool isTransitionOk = false;
        
        /*
         * So there is supposed to be a change.
         * Check if we are allowewd to change.
         */
        switch (_currentState)
        {
            case State.Idle:
                isTransitionOk = true;
                break;
            case State.Conversation:
                isTransitionOk = true;
                break;
            case State.Narration:
                switch (newState)
                {
                    case State.Idle:
                        isTransitionOk = false;
                        break;
                    case State.Conversation:
                        isTransitionOk = false;
                        break;
                    case State.Narration:
                        isTransitionOk = true;
                        break;
                    case State.ScriptedScene:
                        isTransitionOk = true;
                        break;
                }

                break;
            case State.ScriptedScene:
                switch (newState)
                {
                    case State.Idle:
                        isTransitionOk = false;
                        break;
                    case State.Conversation:
                        isTransitionOk = false;
                        break;
                    case State.Narration:
                        isTransitionOk = false;
                        break;
                    case State.ScriptedScene:
                        isTransitionOk = true;
                        break;
                }

                break;
        }

        return isTransitionOk;
    }
    

    private void _computeFlags(State state, ref bool mayConverse, ref bool shallBeInteractive)
    {
        /*
         * Now, that we are allowed to change, set the proper values for some flags.
         */
        switch (_currentState)
        {
            case State.Idle:

                /*
                 * We may start any conversation.
                 */
                mayConverse = true;

                /*
                 * We may ride around, as there is no conversation ongoing.
                 */
                shallBeInteractive = true;

                break;

            case State.Conversation:

                /*
                 * We may start any other conversation, dismisssing the current one.
                 */
                mayConverse = true;

                /*
                 * We may ride around, leaving the conversation partner
                 */
                shallBeInteractive = true;
                
                break;

            case State.Narration:

                /*
                 * We may not start any conversation as long the narration still is going on.
                 */
                mayConverse = false;

                /*
                 * We may ride around, there is no restriction.
                 */
                shallBeInteractive = true;
                
                break;

            case State.ScriptedScene:

                /*
                 * We may not start any conversation as long the scripted scene still is going on.
                 */
                mayConverse = false;

                /*
                 * We may not ride around manually.
                 */
                shallBeInteractive = false;
                
                break;
        }

    }


    private bool _toState(State newState, bool isInternal)
    {
        lock (_lo)
        {
            /*
             * If there is no change, just return success.
             */
            if (newState == _currentState)
            {
                return true;
            }


            bool isTransitionOk = false;
            
            if (isInternal)
            {
                isTransitionOk = true;
            }
            else
            {
                isTransitionOk = _mayTransitionNL(newState);
            }

            if (!isTransitionOk)
            {
                return false;
            }

            _currentState = newState;

            _computeFlags(_currentState, ref _mayConverse, ref _shallBeInteractive);           
        }
        
        I.Get<EventQueue>().Push(new CurrentStateEvent(EventTypeCurrentState, _currentState.ToString())
        {
            MayConverse = _mayConverse,
            ShallBeInteractive = _shallBeInteractive
        });

        return true;
    }


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


    SortedDictionary<string, string> _computeTags(Story story)
    {
        if (story.currentTags != null)
        {
            return new(story.currentTags.Select(x => x.Split(':'))
                .ToDictionary(parts => parts[0], parts => parts[1]));
        }
        else
            return new();
    }
    

    private void _displaySentence()
    {
        _prepareSentence();

        
        string strContent = "";
        string strPerson = "";
        string strAnimation = "";
        
        int nLFs = 0;

        float ytop;
        lock (_lo)
        {
            var tags = _computeTags(_currentStory);
            if (tags.TryGetValue("person", out strPerson))
            {
                // Then we have a person                
            }
            else
            {
                strPerson = "";
            }

            if (tags.TryGetValue("animation", out strAnimation))
            {
                // Then we have an animation to play.
            }
            else
            {
                strAnimation = "";
            }

            /*
             * Emit an event to other systems to let them know who is speaking
             * and what animation is hinted.
             */
            if (!String.IsNullOrWhiteSpace(strPerson))
            {
                I.Get<EventQueue>().Push(new PersonSpeakingEvent(EventTypePersonSpeaking, "")
                {
                    Person = strPerson,
                    Animation = strAnimation
                });
            }
            
            strContent = _currentStory.currentText;
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

        string strDisplay;
        if (String.IsNullOrWhiteSpace(strPerson))
        {
            strDisplay = strContent;
        }
        else
        {
            strDisplay = strPerson + "\n" + strContent;
            nLFs++;
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
    
    
    /**
     * Trigger execution of a given story path.
     *
     * @param strPath
     *     The path as defined in the story
     * @param instanceId
     *     An arbitrary ID passed to the system that will be passed back
     *     in events triggered by this story. Can be used to identify one particular among
     *     lots of similar NPCs.
     */
    private void _triggerPath(string strPath, string instanceId)
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
            _currentInstanceId = instanceId;
        }

        currentStory.ChoosePathString(strPath, true, null);

        _advanceStory();
    }


    public bool MayConverse()
    {
        lock (_lo)
        {
            return _mayConverse;
        }
    }


    public bool ShallBeInteractive()
    {
        lock (_lo)
        {
            return _shallBeInteractive;
        }
    }


    public void TriggerConversation(string strPath, string instanceId)
    {
        if (!_toState(State.Conversation, false))
        {
            Warning($"Tried to trigger conversation {strPath} but was not allowed to.");
            return;
        }

        _triggerPath(strPath, instanceId);
    }


    public void TriggerScriptedScene(string strPath, string instanceId)
    {
        if (!_toState(State.ScriptedScene, false))
        {
            Warning($"Tried to trigger scripted scene {strPath} but was not allowed to.");
            return;
        }

        _triggerPath(strPath, instanceId);
    }


    public void TriggerNarration(string strPath, string instanceId)
    {
        if (!_toState(State.Narration, false))
        {
            Warning($"Tried to trigger narration {strPath} but was not allowed to.");
            return;
        }

        _triggerPath(strPath, instanceId);
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

            try
            {
                using var stream = engine.Assets.Open("story1.json");
                using var sr = new StreamReader(stream, Encoding.UTF8);
                string jsonStory = sr.ReadToEnd();
                _currentStory = new Story(jsonStory);
                _currentStory.BindExternalFunction("triggerQuest",
                    (string questName) => CatchAll(() => I.Get<engine.quest.Manager>().TriggerQuest(questName, true)));
            }
            catch (Exception e)
            {
                Error($"Failed to load story: {e.Message}");
            }
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
        
        // TXWTODO: Today, after loading, we always are in narration of the most recent story. And start telling it again.
        _toState(State.Narration, true);
        
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