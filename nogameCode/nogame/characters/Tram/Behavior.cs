﻿using System;
using nogame.cities;
using System.Numerics;
using engine.behave;
using engine.physics;
using engine.world;

namespace nogame.characters.tram;

internal class Behavior : ABehavior
{
    private readonly engine.Engine _engine;
    private readonly engine.world.ClusterDesc _clusterDesc;
    private readonly StreetNavigationController _snc;
    
    private engine.streets.StreetPoint _streetPoint;
    private Quaternion _qPrevRotation = Quaternion.Identity;
    private float _height;


    public override void Behave(in DefaultEcs.Entity entity, float dt)
    {
        _snc.NavigatorBehave(dt);
        _snc.NavigatorGetTransformation(out var vPosition, out var qOrientation);

        qOrientation = Quaternion.Slerp(_qPrevRotation, qOrientation, 0.1f);
        _qPrevRotation = qOrientation;
        engine.I.Get<engine.joyce.TransformApi>().SetTransforms(
            entity,
            true, 0x0000ffff,
            qOrientation,
            vPosition with
            {
                Y = _clusterDesc.AverageHeight + MetaGen.ClusterNavigationHeight + _height
            }
        );
    }
    

    public Behavior SetSpeed(float speed)
    {
        _snc.Speed = speed;
        return this;
    }
    
    
    public Behavior SetHeight(float height)
    {
        _height = height;
        return this;
    }
    
    
    public override void OnAttach(in engine.Engine engine0, in DefaultEcs.Entity entity)
    {
        base.OnAttach(in engine0, in entity);
        _snc.NavigatorLoad();
    }
    
    
    public Behavior(
        in engine.Engine engine0,
        in engine.world.ClusterDesc clusterDesc0,
        in engine.streets.StreetPoint streetPoint0
    )
    {
        _engine = engine0;
        _clusterDesc = clusterDesc0;
        _streetPoint = streetPoint0;
        _snc = new StreetNavigationController()
        {
            ClusterDesc = clusterDesc0,
            StartPoint = _streetPoint
        };
    }
}