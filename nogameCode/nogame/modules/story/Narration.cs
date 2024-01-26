using System.IO;
using System.Text;
using engine;
using Ink.Runtime;
using static engine.Logger;

namespace nogame.modules.story;

public class Narration : AModule
{
    private Story _currentStory;
    
    private void _advanceStory()
    {
        while (_currentStory.canContinue)
        {
            string strOutput = _currentStory.Continue();
            
            /*
             * Display story item.
             */
            Trace($"story: {strOutput}");
        }

        foreach (var choice in _currentStory.currentChoices)
        {
            Trace($"story.choice: {choice.text}");
        }
    }
    
    private void _triggerNextStory()
    {
        var stream = engine.Assets.Open("story1.json");
        using var sr = new StreamReader(stream, Encoding.UTF8);
        string jsonStory = sr.ReadToEnd();
        _currentStory = new Story(jsonStory);

        _advanceStory();
    }
    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    
    public override void ModuleActivate(engine.Engine engine0)
    {
        base.ModuleActivate(engine0);
        engine0.AddModule(this);

        _triggerNextStory();
    }
}