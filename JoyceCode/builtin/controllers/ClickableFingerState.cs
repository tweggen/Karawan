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


    public override void HandleReleased(Event ev)
    {
        base.HandleReleased(ev);
        
        var factory = Clickable.ClickEventFactory;
        if (factory != null)
        {
            var cev = factory(Entity, ev, PressPosition);
            if (cev != null)
            {
                I.Get<EventQueue>().Push(cev);
            }
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
            var cev = factory(Entity, ev, PressPosition);
            if (cev != null)
            {
                I.Get<EventQueue>().Push(cev);
            }
        }
    }


    public ClickableFingerState(in Vector2 pos, Entity entity, Clickable clickable) : base(in pos)
    {
        Entity = entity;
        Clickable = clickable;
    }
}