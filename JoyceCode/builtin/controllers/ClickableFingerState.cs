using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave.components;
using engine.news;

namespace builtin.controllers;

public class ClickableFingerState : AFingerState
{
    public DefaultEcs.Entity Entity;
    public Clickable Clickable;
    public Vector2 RelPos;


    public override void HandleReleased(Event ev)
    {
        base.HandleReleased(ev);
        
        var factory = Clickable.ClickEventFactory;
        if (factory != null)
        {
            if ((Clickable.Flags & Clickable.ClickableFlags.AlsoOnRelease) != 0)
            {
                // TXWTODO: This is the relative position of the click. 
                var cev = factory(Entity, ev, RelPos);
                if (cev != null)
                {
                    I.Get<EventQueue>().Push(cev);
                }
            }

            ev.IsHandled = true;
        }
    }


    public override void HandleMotion(Event ev)
    {
        base.HandleMotion(ev);
    }


    public override void HandlePressed(Event ev)
    {
        base.HandlePressed(ev);
        
        var factory = Clickable.ClickEventFactory;
        if (factory != null)
        {
            var cev = factory(Entity, ev, RelPos);
            if (cev != null)
            {
                I.Get<EventQueue>().Push(cev);
            }
            ev.IsHandled = true;
        }
    }


    public ClickableFingerState(in Vector2 evPos, Entity entity, Clickable clickable, Vector2 relPos) : base(in evPos)
    {
        Entity = entity;
        Clickable = clickable;
        RelPos = relPos;
    }
}