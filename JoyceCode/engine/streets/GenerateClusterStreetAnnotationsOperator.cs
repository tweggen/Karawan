using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine.draw.components;
using engine.joyce;
using engine.world;
using static engine.Logger;


namespace engine.streets;

public class GenerateClusterStreetAnnotationsOperator : IFragmentOperator
{
    static private object _lock = new();

    private ClusterDesc _clusterDesc;
    private string _myKey;
    private bool _traceStreets = false;

    private engine.joyce.TransformApi _aTransform;

    public string FragmentOperatorGetPath()
    {
        return $"5002/GenerateClusterStreetAnnotationsOperator/{_myKey}/{_clusterDesc.IdString}";
    }


    public void FragmentOperatorGetAABB(out engine.geom.AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    public void _createStreetPointAnnotations(Fragment worldFragment, StrokeStore strokeStore)
    {
        var streetPoints = strokeStore.GetStreetPoints();
        float h = _clusterDesc.AverageHeight + MetaGen.ClusterNavigationHeight;
        /*
         * How high over annotation height shall they be?
         */
        float markerHeight = 1.5f;
        
        foreach (var sp in streetPoints)
        {
            Vector3 markerPos = new(sp.Pos.X + _clusterDesc.Pos.X, h + markerHeight, sp.Pos.Y + _clusterDesc.Pos.Z);
            worldFragment.Engine.QueueEntitySetupAction("streetpoint-annotation", entity =>
            {
                entity.Set(new engine.draw.components.OSDText(
                    new Vector2(0, 30f),
                    new Vector2(160f, 18f),
                    sp.ToString(),
                    12,
                    0x88226622,
                    0x00000000,
                    engine.draw.HAlign.Left)
                {
                    MaxDistance = 200f,
                    OSDTextFlags = OSDText.ENABLE_DISTANCE_FADE
                });
                _aTransform.SetTransforms(
                    entity,
                    true, 0x0000ffff,
                    Quaternion.Identity, 
                    markerPos
                );
            });
        }
    }


    public Func<Task> FragmentOperatorApply(world.Fragment worldFragment, FragmentVisibility visib) => new (async () =>
    {
        if (0 == (visib.How & engine.world.FragmentVisibility.Visible3dAny))
        {
            return;
        }
        
        // Perform clipping until we have bounding boxes

        float cx = _clusterDesc.Pos.X - worldFragment.Position.X;
        float cz = _clusterDesc.Pos.Z - worldFragment.Position.Z;

        /*
         * We don't apply the operator if the fragment completely is
         * outside our boundary box (the cluster)
         */
        {
            {
                float csh = _clusterDesc.Size / 2.0f;
                float fsh = world.MetaGen.FragmentSize / 2.0f;
                if (
                    (cx - csh) > (fsh)
                    || (cx + csh) < (-fsh)
                    || (cz - csh) > (fsh)
                    || (cz + csh) < (-fsh)
                )
                {
                    if (_traceStreets) Trace("Too far away: x=" + _clusterDesc.Pos.X + ", z=" + _clusterDesc.Pos.Z);
                    return;
                }
            }
        }

        var strokeStore = _clusterDesc.StrokeStore();

        if (_traceStreets)
            Trace($"In terrain '{worldFragment.GetId()}' operator. "
                  + $"Fragment @{worldFragment.Position}. "
                  + $"Cluster '{_clusterDesc.IdString}' @{cx}, {cz}, R:{_clusterDesc.Size}.");

        _createStreetPointAnnotations(worldFragment, strokeStore);
    });
    

    public GenerateClusterStreetAnnotationsOperator(
        in ClusterDesc clusterDesc,
        in string strKey
    )
    {
        _clusterDesc = clusterDesc;
        _aTransform = I.Get<engine.joyce.TransformApi>();
 
        I.Get<ObjectRegistry<Material>>().RegisterFactory("engine.streets.materials.street",
            (name) => new Material()
            {
                AlbedoColor = (bool) engine.Props.Get("debug.options.flatshading", false) != true
                    ? 0x00000000 : 0xff888888,
                Texture = new engine.joyce.Texture("streets1to4.png")
            });
    }
    
    
    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new GenerateClusterStreetAnnotationsOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}