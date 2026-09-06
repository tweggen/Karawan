using System.Collections.Generic;
using engine.world;
using LiteDB;

namespace engine.streets;

public class ClusterStorage
{
    private object _lo = new();
    
    private engine.EntityMap<engine.streets.StreetPoint> _mapStreetPoints = new();

    /**
     * ⚠️ BUMPING THIS DELETES THE WHOLE worldcache FILE, not just the street collections.
     *
     * DBStorage._open deletes the file outright when its UserVersion is below the version
     * asked for, and GenerateClustersOperator stores the CLUSTER LIST in that same file -
     * so a bump throws away every player's and every developer's cached world, cities
     * included, and the next start regenerates all of it.
     *
     * 1040 is WP-B6, joyce.EnableGradeSeparation becoming the default: strokes carry a
     * Kind and a Level that the stored network did not have, and a cached city built
     * without them would come back as a flat network in a game that now expects bridges.
     * The cluster list itself is a pure function of its seed and comes back identical -
     * asserted, not assumed, by ClusterListSurvivesTests.
     */
    public const int DbVersion = 1040;

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
            strokeStore.AddStoredStroke(stroke);
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