using System;
using System.Collections.Generic;
using System.Xml;
using builtin.jt;
using builtin.modules.inventory;
using builtin.modules.inventory.components;
using engine;
using engine.news;
using engine.world.components;
using static engine.Logger;

namespace nogame.modules.shop;


public class Module : AModule, IInputPart
{
    public LayerDefinition _layerDefinition;


    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<builtin.jt.Factory>(),
        new SharedModule<InputEventPipeline>(),
        new SharedModule<LayerCatalogue>()
    };

    
    /**
     * This input handler exists purely for closing the osd again. using escape.
     */
    public void InputPartOnInputEvent(Event ev)
    {
        bool doDeactivate = false;

        switch (ev.Type)
        {
            case Event.INPUT_BUTTON_PRESSED:
                switch (ev.Code)
                {
                    case "<menu>":
                        doDeactivate = true;
                        break;
                    
                    case "<interact>":
                        /*
                       * We just consume the interact key to avoid reopening the shop.
                       * Unsure who really should own it. 
                       */
                        ev.IsHandled = true;
                        break;
                }
                break;
        }

        if (doDeactivate)
        {
            ev.IsHandled = true;
            ModuleDeactivate();
        }

    }

    
    private void _buy(Event ev)
    {
        ev.IsHandled = true;
        /*
         * Do we know the item?
         */
        if (!I.Get<PickableDirectory>().Has(ev.Code))
        {
            Error($"Configuration error: Unable to find item with the given code.");
            return;
        }
        
        /*
         * Add an item to the world.
         */
        _engine.QueueEntitySetupAction("item", (e) =>
        {
            // TXWTODO: Create new pickable right here.
            e.Set(new Pickable()
            {
                Description = I.Get<PickableDirectory>().Get(ev.Code)
            });
            e.Set(new Creator(Creator.CreatorId_Hardcoded));
        });
    }

    private void _closeShop(Event ev)
    {
        ev.IsHandled = true;
        ModuleDeactivate();
    }
    

    protected override void OnModuleDeactivate()
    {
        I.Get<SubscriptionManager>().Unsubscribe("nogame.modules.shop.close", _closeShop);

        M<InputEventPipeline>().RemoveInputPart(this);

        M<Factory>().CloseOSD(_layerDefinition.Name, "shopFront");
        _engine.GamePlayState = GamePlayStates.Running;
    }


    protected override void OnModuleActivate()
    {
        base.OnModuleActivate();
        
        _engine.GamePlayState = GamePlayStates.Paused;

        I.Get<SubscriptionManager>().Subscribe("nogame.modules.shop.close", _closeShop);
        I.Get<SubscriptionManager>().Subscribe("nogame.modules.shop.buy", _buy);

        try
        {
            var wShop = M<Factory>().OpenOSD("shop.xml", "shopFront");
            _layerDefinition = M<LayerCatalogue>().Get(wShop["layer"].ToString());
        }
        catch (Exception e)
        {
            Error($"Exception opening menu: {e}");
        }

        M<InputEventPipeline>().AddInputPart(_layerDefinition.ZOrder, this);
    }
}
