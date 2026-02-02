using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.draw;
using engine.draw.components;
using engine.narration;
using engine.news;
using static engine.Logger;

namespace nogame.modules.story;


/// <summary>
/// UI layer for the narration system. Delegates all narration logic to
/// NarrationManager (engine-level) and handles input and OSD display.
///
/// This replaces the previous Ink-based Narration module.
/// </summary>
public class Narration : AModule, IInputPart
{
    /// <summary>
    /// Legacy event type constants for backward compatibility with ANearbyBehavior etc.
    /// </summary>
    public static readonly string EventTypePersonSpeaking = SpeakerChangedEvent.EVENT_TYPE;
    public static readonly string EventTypeCurrentState = NarrationStateEvent.EVENT_TYPE;

    private int _currentNChoices = 0;
    private int _chosenOption = 0;

    private Boom.ISound _soundTty = null;

    private DefaultEcs.Entity _eSentence = default;
    private List<DefaultEcs.Entity> _listEOptions;

    public float MY_Z_ORDER { get; set; } = 24.5f;

    public float BottomY { get; set; } = (400f - 20f);
    public float LineHeight { get; set; } = 16f + 4f;

    public uint TextColor { get; set; } = 0xffcccccc;
    public uint TextFill { get; set; } = 0x00000000;
    public uint ChoiceColor { get; set; } = 0xffbbdddd;
    public uint ChoiceFill { get; set; } = 0x00000000;

    private NarrationRunner.NodeResult _currentResult;


    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<AutoSave>(),
        new SharedModule<nogame.modules.osd.Display>(),
        new SharedModule<InputEventPipeline>(),
        new SharedModule<Saver>(),
        new SharedModule<NarrationManager>()
    };


    /// <summary>
    /// Public API: trigger a conversation (for backward compat with ANearbyBehavior).
    /// </summary>
    public void TriggerConversation(string scriptName, string instanceId)
    {
        _engine.QueueMainThreadAction(async () =>
        {
            await M<NarrationManager>().TriggerScript(scriptName, "conversation", instanceId);
        });
    }


    /// <summary>
    /// Public API: trigger a narration.
    /// </summary>
    public void TriggerNarration(string scriptName, string instanceId)
    {
        _engine.QueueMainThreadAction(async () =>
        {
            await M<NarrationManager>().TriggerScript(scriptName, "narration", instanceId);
        });
    }


    /// <summary>
    /// Public API: trigger a scripted scene.
    /// </summary>
    public void TriggerScriptedScene(string scriptName, string instanceId)
    {
        _engine.QueueMainThreadAction(async () =>
        {
            await M<NarrationManager>().TriggerScript(scriptName, "scriptedScene", instanceId);
        });
    }


    public bool MayConverse()
    {
        return M<NarrationManager>().MayConverse;
    }


    public bool ShallBeInteractive()
    {
        return M<NarrationManager>().ShallBeInteractive;
    }


    #region Display

    private void _prepareSentence()
    {
        if (_eSentence.IsAlive) return;
        var mDisplay = M<nogame.modules.osd.Display>();
        _eSentence = _engine.CreateEntity("nogame.modules.story sentence");
        _eSentence.Set(new OSDText(
            new Vector2((mDisplay.Width - 500f) / 2f, BottomY - LineHeight),
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
        eOption.Set(new OSDText(
            new Vector2((mDisplay.Width - 500f) / 2f, y),
            new Vector2(500f, LineHeight),
            text,
            16,
            ChoiceColor,
            ChoiceFill,
            HAlign.Center,
            VAlign.Top));
        eOption.Set(new engine.behave.components.Clickable()
        {
            ClickEventFactory = (e, cev, v2RelPos) => new Event("nogame.modules.story.sentence.onClick", $"{idx + 1}")
        });
        return eOption;
    }


    private void _updateOptions()
    {
        if (_currentNChoices > 0 && _listEOptions != null)
        {
            int idx = 0;
            foreach (var eChoice in _listEOptions)
            {
                if (eChoice.IsAlive)
                {
                    eChoice.Get<OSDText>().BorderColor = (idx == _chosenOption) ? ChoiceColor : 0u;
                }

                idx++;
            }
        }
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
            _eSentence.Get<OSDText>().Text = "";
        }
    }


    private void _dismissDisplay()
    {
        _dismissSentence();
        _dismissChoices();
        _currentResult = null;
        _currentNChoices = 0;
        _chosenOption = 0;
    }


    private int _countLF(string str)
    {
        int count = 0;
        int length = str.Length;
        while (length > 0 && str[length - 1] == '\n')
        {
            --length;
        }

        for (int i = 0; i < length; ++i)
        {
            if (str[i] == '\n') count++;
        }

        return count;
    }


    private void _displayNodeResult(NarrationRunner.NodeResult result)
    {
        if (result == null)
        {
            _dismissDisplay();
            return;
        }

        _currentResult = result;
        _prepareSentence();

        string strContent = result.Text;
        string strPerson = result.Speaker ?? "";
        int nLFs = _countLF(strContent);

        _currentNChoices = result.Choices?.Count ?? 0;
        _chosenOption = 0;

        float ytop = BottomY - LineHeight * (nLFs + _currentNChoices + 1);

        if (_currentNChoices > 0)
        {
            _dismissChoices();
            _listEOptions = new();
            for (int i = 0; i < _currentNChoices; ++i)
            {
                var eChoice = _createOptionEntity(i, ytop + LineHeight * (i + 1f), $"{i + 1}) " + result.Choices[i]);
                _listEOptions.Add(eChoice);
            }
        }
        else
        {
            _dismissChoices();
        }

        string strDisplay;
        if (string.IsNullOrWhiteSpace(strPerson))
        {
            strDisplay = strContent;
        }
        else
        {
            strDisplay = strPerson + "\n" + strContent;
            nLFs++;
        }

        ref var cSentenceOSDText = ref _eSentence.Get<OSDText>();
        cSentenceOSDText.Text = strDisplay;
        cSentenceOSDText.Position.Y = ytop;
        cSentenceOSDText.Size.Y = (nLFs + 1) * LineHeight;

        if (_soundTty != null)
        {
            _soundTty.Stop();
            _soundTty.Volume = 0.02f;
            _soundTty.Play();
        }

        _updateOptions();
    }

    #endregion


    #region Input

    public void InputPartOnInputEvent(Event ev)
    {
        var manager = M<NarrationManager>();
        if (manager.IsIdle() || manager.ActiveRunner == null)
        {
            return;
        }

        bool doSelect = false;
        bool doPrevious = false;
        bool doNext = false;

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
                    if (ev.Code.CompareTo("0") >= 0 && ev.Code.CompareTo("9") <= 0)
                    {
                        int number = ev.Code[0] - '0';
                        if (0 == number) number = 10;
                        if (_currentNChoices > 0 && number <= _currentNChoices)
                        {
                            ev.IsHandled = true;
                            _doChoose(number);
                            return;
                        }
                    }

                    break;
            }
        }

        if (doSelect)
        {
            ev.IsHandled = true;
            if (_currentNChoices == 0)
            {
                _doAdvance();
            }
            else
            {
                _doChoose(_chosenOption + 1);
            }
        }

        if (_currentNChoices > 0 && (doPrevious || doNext))
        {
            ev.IsHandled = true;
            if (doPrevious) _chosenOption = (_chosenOption + _currentNChoices - 1) % _currentNChoices;
            if (doNext) _chosenOption = (_chosenOption + 1) % _currentNChoices;
            _updateOptions();
        }
    }


    private void _doAdvance()
    {
        _engine.QueueMainThreadAction(async () =>
        {
            var result = await M<NarrationManager>().Advance();
            _displayNodeResult(result);
        });
    }


    private void _doChoose(int number)
    {
        _engine.QueueMainThreadAction(async () =>
        {
            var result = await M<NarrationManager>().Choose(number - 1);
            _displayNodeResult(result);
        });
    }

    #endregion


    private void _onClickSentence(Event ev)
    {
        var manager = M<NarrationManager>();
        if (manager.IsIdle()) return;

        if (ev.Code != null)
        {
            if (Int32.TryParse(ev.Code, out var number))
            {
                _doChoose(number);
            }
        }
        else
        {
            if (_currentNChoices == 0)
            {
                _doAdvance();
            }
        }
    }


    private void _onNodeReached(Event ev)
    {
        if (ev is NodeReachedEvent nodeEv)
        {
            _engine.QueueMainThreadAction(() =>
            {
                // Build a NodeResult-like display from the event
                var result = new NarrationRunner.NodeResult
                {
                    NodeId = nodeEv.NodeId,
                    Text = nodeEv.InterpolatedText,
                    Speaker = nodeEv.Speaker,
                    Animation = nodeEv.Animation,
                    Choices = nodeEv.InterpolatedChoices ?? new(),
                    Events = new(),
                    HasChoices = (nodeEv.InterpolatedChoices?.Count ?? 0) > 0,
                    HasGoto = true
                };
                _displayNodeResult(result);
            });
        }
    }


    private void _onScriptEnded(Event ev)
    {
        _engine.QueueMainThreadAction(() => { _dismissDisplay(); });
    }


    protected override void OnModuleDeactivate()
    {
        M<InputEventPipeline>().RemoveInputPart(this);
        M<Saver>().OnAfterLoadGame -= _onAfterLoadGame;
    }


    protected override void OnModuleActivate()
    {
        if (null == _soundTty)
        {
            try
            {
                _soundTty = I.Get<Boom.ISoundAPI>().FindSound("terminal.ogg");
            }
            catch (Exception)
            {
                // Sound may not be available
            }
        }

        M<InputEventPipeline>().AddInputPart(MY_Z_ORDER, this);
        M<Saver>().OnAfterLoadGame += _onAfterLoadGame;

        // Register game-specific narration bindings
        NarrationBindings.Register(M<NarrationManager>());

        Subscribe("nogame.modules.story.sentence.onClick", _onClickSentence);
        Subscribe(NodeReachedEvent.EVENT_TYPE, _onNodeReached);
        Subscribe(ScriptEndedEvent.EVENT_TYPE, _onScriptEnded);
    }


    private void _onAfterLoadGame(object sender, object objGameState)
    {
        // After loading, check if NarrationManager has restored state and display it.
        var manager = M<NarrationManager>();
        if (manager.ActiveRunner != null && !manager.ActiveRunner.IsFinished)
        {
            Task.Delay(5000).ContinueWith(t =>
            {
                _engine.QueueMainThreadAction(() =>
                {
                    // Re-display current node if runner is active
                    // The NodeReachedEvent will handle display via subscription
                });
            });
        }
    }
}
