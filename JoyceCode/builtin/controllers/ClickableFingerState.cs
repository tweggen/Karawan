using System;
using System.Numerics;
using DefaultEcs;
using engine;
using engine.behave.components;
using engine.news;
using static engine.Logger;

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
                try
                {
                    // TXWTODO: This is the relative position of the click. 
                    var cev = factory(Entity, ev, RelPos);
                    if (cev != null)
                    {
                        I.Get<EventQueue>().Push(cev);
                    }
                } catch (Exception e)
                {
                    Warning($"Caught exception while executing click event factory Release: {e}.");
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
            try
            {
                var cev = factory(Entity, ev, RelPos);
                if (cev != null)
                {
                    I.Get<EventQueue>().Push(cev);
                }
            }
            catch (Exception e)
            {
                Warning($"Caught exception while executing click event factory Press: {e}.");
            }

            ev.IsHandled = true;
        }
    }


    /**
     * @param evPos
     *     The position used to track motion
     * @param entity
     *     The actual entity that is or is not movable.
     * @param clickable
     *     The description of the clickable
     * @param relPos
     *     The position relative to the clickable that is passed to
     *     the handler.
     */
    public ClickableFingerState(in Vector2 evPos, Entity entity, Clickable clickable, Vector2 relPos) : base(in evPos)
    {
        Entity = entity;
        Clickable = clickable;
        RelPos = relPos;
    }
}