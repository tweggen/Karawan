using System.Collections.Generic;
using engine.world;
using LiteDB;

namespace engine.streets;

public class ClusterStorage
{
    private object _lo = new();
    
    private engine.EntityMap<engine.streets.StreetPoint> _mapStreetPoints = new();

    public const int DbVersion = 1015;
    
    public const string DbName = "worldcache";

    public bool TryLoadClusterStreets(ClusterDesc clusterDesc)
    {
        List<Stroke> strokes = null;

        bool haveStrokes = false;
        I.Get<DBStorage>().WithOpen(DbName, DbVersion, db =>
        {
            if (!db.CollectionExists("Stroke") || !db.CollectionExists("StreetPoint")) return;
            var col = db.GetCollection<Stroke>().Include(x => x.A).Include(x => x.B);
            var enumStroke = col.Find(stroke => stroke.ClusterId == clusterDesc.Id);
            strokes = new List<Stroke>(enumStroke);
            haveStrokes = true;
        });
        if (!haveStrokes) return false;
        if (null == strokes ||0 == strokes.Count) return false;

        StrokeStore strokeStore = clusterDesc.StrokeStore();
        foreach (var stroke in strokes)
        {
            strokeStore.AddStroke(stroke);
        }
        
        return true;
    }
    
    
    public void StoreClusterStreetPoints(ClusterDesc clusterDesc)
    {
        var dbs = I.Get<DBStorage>(); 
        var streetPoints = clusterDesc.StrokeStore().GetStreetPoints();
        dbs.WithOpen(DbName, DbVersion,
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
    
    
    public void StoreClusterStrokes(ClusterDesc clusterDesc)
    {
        var dbs = I.Get<DBStorage>(); 
        var strokes = clusterDesc.StrokeStore().GetStrokes();
        dbs.WithOpen(DbName, DbVersion,
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


    public StreetPoint _createStreetPoint(BsonDocument doc)
    {
        int streetPointId = doc["_id"].AsInt32;
        var streetPoint = _mapStreetPoints.Find(streetPointId);
        streetPoint.Id = streetPointId;
        streetPoint.ClusterId = doc["ClusterId"].AsInt32;
        return streetPoint;
    }


    public ClusterStorage()
    {
        I.Get<DBStorage>().Mapper.Entity<StreetPoint>().Ctor(_createStreetPoint);
    }
}