using System;
using engine.news;
using static engine.Logger;

namespace engine;

public class Saver : AModule
{
    private int _maySave = 0;
    public bool MaySave
    {
        get
        {
            lock (_lo)
            {
                return _maySave == 0;
            }
        }
    }
    

    public event EventHandler<object> OnBeforeSaveGame;

    public Action SaveAction;

    public event EventHandler<object> OnAfterLoadGame;


    public void EnableSave()
    {
        lock (_lo)
        {
            if (0 == _maySave)
            {
                Error($"Error: Maysave already was zero while trying to enable safe.");
            }
            _maySave--;
        }
    }


    public void DisableSave()
    {
        lock (_lo)
        {
            _maySave++;
        }
    }


    public void Save(string reason)
    {
        if (!MaySave)
        {
            return;
        }

        _engine.RunMainThread(() =>
        {

            OnBeforeSaveGame?.Invoke(this, reason);
            SaveAction();
        });
    }


    public void CallAfterLoad(object gs)
    {
        OnAfterLoadGame?.Invoke(this, gs);
    }

    private void _handleTriggerSave(Event ev)
    {
        Save(ev.Code);
    }


    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    

    public override void ModuleActivate()
    {
        base.ModuleActivate();
        I.Get<SubscriptionManager>().Subscribe("builtin.SaveGame.TriggerSave", _handleTriggerSave);
        _engine.AddModule(this);
    }
}