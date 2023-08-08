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
public class TitlePart : engine.IPart
{
    private object _lo = new();
    private engine.Engine _engine;
    private engine.IScene _scene;

    private List<TitleCard> _cards = new();
    private Dictionary<TitleCard, ActiveCardEntry> _dictCards = new();


    private void _computeTransformAt(TitleCard card, double t, out engine.transform.components.Transform3 t3)
    {
        t3 = new(
            true, card.StartTransform.CameraMask,
            Quaternion.Slerp(
                card.StartTransform.Rotation, card.EndTransform.Rotation,
                (float)t),
            card.StartTransform.Position +
            (card.EndTransform.Position - card.StartTransform.Position) * (float)t
        );
    }


    private void _onCardStart(TitleCard card)
    {
        Trace("Card Start.");
        var now = DateTime.Now;

        _computeTransformAt(card, 0f, out engine.transform.components.Transform3 t3);

        engine.joyce.Mesh mesh = engine.joyce.mesh.Tools.CreatePlaneMesh(
            "TitleCard", card.Size, card.PosUV, card.SizeUV with { Y = 0 }, card.SizeUV with { X = 0 }
        );
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
            InstanceDesc = engine.joyce.InstanceDesc.CreateFromMatMesh(new MatMesh(mat, mesh))
        };

        _engine.QueueEntitySetupAction("titlecard", (DefaultEcs.Entity entity) =>
        {
            ace.Entity = entity;
            entity.Set(new engine.joyce.components.Instance3(ace.InstanceDesc));
            entity.Set(t3);
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
        }

        if (ace != null)
        {
            _engine.QueueMainThreadAction(() =>
            {
                ace.Entity.Dispose();
                // ace.InstanceDesc.Dispose();
            });
        }

        _engine.RemovePart(this);
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
            float t = (float)(ace.StartTime - now).TotalMilliseconds;
            _computeTransformAt(ace.Card, t, out engine.transform.components.Transform3 t3);
            ace.Entity.Set(t3);
        }
    }


    public void PartOnKeyEvent(KeyEvent _)
    {
    }


    public void PartDeactivate()
    {
        _engine.RemovePart(this);
        _engine.LogicalFrame -= _onLogicalFrame;
    }


    /**
     * Immediately after activation, the timepoints are registered.
     */
    public void PartActivate(in Engine engine0, in IScene scene0)
    {
        _scene = scene0;
        _engine = engine0;

        _engine.AddPart(500, _scene, this);

        var timeline = Implementations.Get<Timeline>();

        foreach (var card in _cards)
        {
            timeline.RunAt(card.StartReference, card.StartOffset, () => { _onCardStart(card); });
            timeline.RunAt(card.EndReference, card.EndOffset, () => { _onCardStop(card); });
        }

        _engine.LogicalFrame += _onLogicalFrame;
    }


    public void Add(TitleCard card)
    {
        lock (_lo)
        {
            _cards.Add(card);
        }
    }


    public TitlePart(TitleCard card)
    {
        Add(card);
    }
}