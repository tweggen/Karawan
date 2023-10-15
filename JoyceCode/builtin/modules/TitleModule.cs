using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using engine;
using engine.joyce;
using engine.news;
using static engine.Logger;

namespace builtin.parts;


class ActiveCardEntry
{
    public TitleCard Card;
    public DateTime StartTime;
    public DefaultEcs.Entity Entity;
    public engine.joyce.InstanceDesc InstanceDesc;
}


/**
 * Implement a part that displays a title card.
 */
public class TitleModule : engine.AModule
{
    private object _lo = new();
    private engine.Engine _engine;
    private List<TitleCard> _cards = new();
    private Dictionary<TitleCard, ActiveCardEntry> _dictCards = new();


    private void _computeTransformAt(TitleCard card, double t, out engine.joyce.components.Transform3 t3)
    {
        t3 = new(
            true, card.StartTransform.CameraMask,
            Quaternion.Slerp(
                card.StartTransform.Rotation, card.EndTransform.Rotation,
                (float)t),
            card.StartTransform.Position +
            (card.EndTransform.Position - card.StartTransform.Position) * ((float)t/1000f)
        );
    }


    private void _onCardStart(TitleCard card)
    {
        Trace("Card Start.");
        var now = DateTime.Now;

        _computeTransformAt(card, 0f, out engine.joyce.components.Transform3 t3);

        engine.joyce.Mesh mesh = engine.joyce.mesh.Tools.CreatePlaneMesh(
            "TitleCard", card.Size, card.PosUV, card.SizeUV with { Y = 0 }, card.SizeUV with { X = 0 }
        );
        mesh.UploadImmediately = true;
        engine.joyce.Material mat = new()
        {
            Texture = card.AlbedoTexture,
            EmissiveTexture = card.EmissiveTexture,
            HasTransparency = true,
            UploadImmediately = true
        };

        var ace = new ActiveCardEntry()
        {
            Card = card,
            StartTime = now,
            InstanceDesc = engine.joyce.InstanceDesc.CreateFromMatMesh(new MatMesh(mat, mesh), 100f)
        };

        _engine.QueueEntitySetupAction("titlecard", (DefaultEcs.Entity entity) =>
        {
            ace.Entity = entity;
            I.Get<engine.joyce.TransformApi>().SetTransforms(entity, t3.IsVisible, t3.CameraMask, t3.Rotation, t3.Position);
            entity.Set(new engine.joyce.components.Instance3(ace.InstanceDesc));
            lock (_lo)
            {
                _dictCards.Add(card, ace);
            }
        });
    }


    private void _onCardStop(TitleCard card)
    {
        Trace("Card Stop.");
        ActiveCardEntry ace = null;
        lock (_lo)
        {
            _dictCards.TryGetValue(card, out ace);
            _dictCards.Remove(card);
        }

        if (ace != null)
        {
            _engine.AddDoomedEntity(ace.Entity);
        }
    }


    private void _onLogicalFrame(object? sender, float dt)
    {
        List<ActiveCardEntry> activeCards = new();
        lock (_lo)
        {
            foreach (var kvp in _dictCards)
            {
                activeCards.Add(kvp.Value);
            }
        }

        DateTime now = DateTime.Now;
        foreach (var ace in activeCards)
        {
            float t = (float)(now- ace.StartTime).TotalMilliseconds;
            _computeTransformAt(ace.Card, t, out engine.joyce.components.Transform3 t3);
            I.Get<engine.joyce.TransformApi>().SetTransforms(ace.Entity, t3.IsVisible, t3.CameraMask, t3.Rotation, t3.Position);
#if false
            {
                float inFadeOut = t - ((float)ace.Card.Duration - ace.Card.FadeOutTime*1000f);
                if (inFadeOut < 0f)
                {
                    /*
                     * already was setup that way.
                     */
                    // ace.InstanceDesc.Materials[0].EmissiveFactors = 0xffffffff;
                }
                else if (inFadeOut<1000f)
                {
                    byte fade = (byte)((1f - inFadeOut) * 255f);
                    ace.InstanceDesc.Materials[0].EmissiveFactors = (uint)fade << 24;
                }
                else
                {
                    ace.InstanceDesc.Materials[0].EmissiveFactors = 0;
                }
            }
#endif
        }
    }


    public void ModuleDeactivate()
    {
        foreach (var card in _cards)
        {
            _onCardStop(card);
        }
        _engine.RemoveModule(this);
        _engine.OnLogicalFrame -= _onLogicalFrame;
    }


    /**
     * Immediately after activation, the timepoints are registered.
     */
    public void ModuleActivate(Engine engine0)
    {
        _engine = engine0;

        _engine.AddModule(this);

        var timeline = I.Get<Timeline>();

        foreach (var card in _cards)
        {
            timeline.RunAt(card.StartReference, card.StartOffset, () => { _onCardStart(card); });
            timeline.RunAt(card.EndReference, card.EndOffset, () => { _onCardStop(card); });
        }

        _engine.OnLogicalFrame += _onLogicalFrame;
    }


    public void Dispose()
    {
        
    }
    

    public void Add(TitleCard card)
    {
        lock (_lo)
        {
            _cards.Add(card);
        }
    }
}