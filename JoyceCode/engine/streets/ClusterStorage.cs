using System.Collections.Generic;
using engine.world;
using LiteDB;

namespace engine.streets;

public class ClusterStorage
{
    static public bool TryLoadClusterStreets(ClusterDesc clusterDesc)
    {
        var dbStorage = I.Get<DBStorage>();
        IEnumerable<StreetPoint> streetPoints = null; 
        bool haveIt = dbStorage.LoadCollection(
            streetPoint => streetPoint.ClusterId == clusterDesc.Id,
            out streetPoints);
        if (!haveIt) return false;
        return true;
    }
    
    
    static public void StoreClusterStreetPoints(ClusterDesc clusterDesc)
    {
        var streetPoints = clusterDesc.StrokeStore().GetStreetPoints();
        I.Get<DBStorage>().UpdateCollection(streetPoints, col =>
        {
            /*
             * Before adding the new items, delete the previous ones.
             */
            col.EnsureIndex(x => x.ClusterId);
            col.DeleteMany(streetPoint => streetPoint.ClusterId == clusterDesc.Id);
        });
    }
    
    
    static public void StoreClusterStrokes(ClusterDesc clusterDesc)
    {
        var strokes = clusterDesc.StrokeStore().GetStrokes();
        I.Get<DBStorage>().UpdateCollection(strokes, col =>
        {
            /*
             * Before adding the new items, delete the previous ones.
             */
            col.EnsureIndex(x => x.ClusterId);
            col.DeleteMany(stroke => stroke.ClusterId == clusterDesc.Id);
        });
    }
}