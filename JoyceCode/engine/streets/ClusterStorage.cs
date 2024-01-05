using System.Collections.Generic;
using engine.world;
using LiteDB;

namespace engine.streets;

public class ClusterStorage
{
    public const string DbWorldCache = "worldcache"; 

    static public bool TryLoadClusterStreets(ClusterDesc clusterDesc)
    {
        IEnumerable<Stroke> strokes = null;

        bool haveStrokes = false;
        I.Get<DBStorage>().WithOpen(DbWorldCache, db =>
        {
            if (!db.CollectionExists("Stroke") || !db.CollectionExists("StreetPoint")) return;
            var col = db.GetCollection<Stroke>().Include("StreetPoint");
            var enumStroke = col.Find(stroke => stroke.ClusterId == clusterDesc.Id);
            strokes = new List<Stroke>(enumStroke);
            haveStrokes = true;
        });
        if (!haveStrokes) return false;

        StrokeStore strokeStore = clusterDesc.StrokeStore();
        foreach (var stroke in strokes)
        {
            strokeStore.AddStroke(stroke);
        }
        
        return true;
    }
    
    
    static public void StoreClusterStreetPoints(ClusterDesc clusterDesc)
    {
        var dbs = I.Get<DBStorage>(); 
        var streetPoints = clusterDesc.StrokeStore().GetStreetPoints();
        dbs.WithOpen(DbWorldCache,
            db =>
            {
                dbs.WithCollection<StreetPoint>(db, col =>
                {
                    /*
                     * Before adding the new items, delete the previous ones.
                     */
                    col.EnsureIndex(x => x.ClusterId);
                    col.DeleteMany(streetPoint => streetPoint.ClusterId == clusterDesc.Id);
                    col.Insert(streetPoints);
                });
                db.Commit();
            });
    }
    
    
    static public void StoreClusterStrokes(ClusterDesc clusterDesc)
    {
        var dbs = I.Get<DBStorage>(); 
        var strokes = clusterDesc.StrokeStore().GetStrokes();
        dbs.WithOpen(DbWorldCache,
            db =>
            {
                dbs.WithCollection<Stroke>(db, col =>
                {
                    /*
                     * Before adding the new items, delete the previous ones.
                     */
                    col.EnsureIndex(x => x.ClusterId);
                    col.DeleteMany(stroke => stroke.ClusterId == clusterDesc.Id);
                    col.Insert(strokes);
                });
                db.Commit();
            });
    }
}