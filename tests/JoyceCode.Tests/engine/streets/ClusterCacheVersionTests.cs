using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using engine;
using engine.streets;
using engine.world;
using Xunit;

namespace JoyceCode.Tests.engine.streets;


/**
 * WP-B6.3 — bumping ClusterStorage.DbVersion throws the whole world cache away.
 *
 * ⚠️ NOT JUST THE STREETS. DBStorage._open deletes the FILE when its UserVersion is below
 * the version asked for, and GenerateClustersOperator stores the CLUSTER LIST in that same
 * file under the same name - so the bump that makes a cached ground-only network
 * unreadable also throws away the seventy cities of the world, for every player and every
 * developer. The plan asks for that to be verified rather than assumed.
 *
 * ⚠️ IT IS VERIFIED AND THE ANSWER IS ODDER THAN THE QUESTION: the cluster list is not in
 * the cache in the first place. ClusterDesc serialises its StreetHeightSource, and
 * FlatStreetHeight has no parameterless constructor, so LiteDB writes the seventy cities
 * happily and throws on the way back:
 *
 *     LiteException: Failed to create instance for type
 *     'engine.streets.FlatStreetHeight' ... Checks if the class has a public constructor
 *     with no parameters.
 *
 * and DBStorage._readCollection's inner catch then DROPS THE COLLECTION and returns false,
 * with no log line of any kind - not even a Trace. So every start has always regenerated
 * the cluster list, the bump cannot lose what was never kept, and what makes the world
 * stable across it is that _generateClusterList is a pure function of its seed. That is
 * the property this file asserts, and the drop is asserted too so that repairing it is
 * visible rather than a surprise.
 *
 * The database name is this file's own so that nothing else can be reading or writing it,
 * and it is removed before and after.
 */
public class ClusterCacheVersionTests
{
    private const string DbName = "wp-b6-clusterlist-probe";


    /**
     * Something that round-trips, for the tests that are about the FILE rather than about
     * ClusterDesc.
     */
    public class Marker
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }


    private static string _pathOf()
        => Path.Combine(GlobalSettings.Get("Engine.RWPath") ?? "", DbName + ".db");


    private static List<ClusterDesc> _generate()
    {
        var op = new GenerateClustersOperator("mydear");
        op._generateClusterList(null, out var list);

        return new List<ClusterDesc>(list);
    }


    private static void _assertSame(IList<ClusterDesc> expected, IList<ClusterDesc> got)
    {
        Assert.Equal(expected.Count, got.Count);

        for (int i = 0; i < expected.Count; ++i)
        {
            Assert.Equal(expected[i].Id, got[i].Id);
            Assert.Equal(expected[i].IdString, got[i].IdString);
            Assert.Equal(expected[i].Name, got[i].Name);
            Assert.Equal(expected[i].Size, got[i].Size);
            Assert.Equal(expected[i].Pos, got[i].Pos);
        }
    }


    /**
     * ⚠️ THE PROPERTY THE BUMP RESTS ON: the cluster list is a pure function of its seed.
     *
     * Everything else here is about a cache that turns out not to work. This is why that
     * does not matter: two runs of the operator's own generator lay the same seventy cities
     * with the same ids at the same places at the same sizes, so a world cache thrown away
     * comes back as the same world.
     */
    [Fact]
    public void TheClusterListIsAPureFunctionOfItsSeed()
    {
        var first = _generate();
        var second = _generate();

        Assert.Equal(70, first.Count);
        _assertSame(first, second);
    }


    /**
     * ⚠️ FOUND WHILE VERIFYING B6.3 AND NOT FIXED: the world cache cannot hold a cluster
     * list, and failing to read one deletes it without a word.
     *
     * The write works - seventy documents land in the file, which this reads back with a
     * plain LiteDB connection to be sure. The typed read then throws on FlatStreetHeight
     * and DBStorage drops the collection, so the next start finds nothing and regenerates.
     *
     * Fixing it is a change to what ClusterDesc persists and belongs with whoever wants the
     * cache to work; recording it here is what makes "the cluster list comes back identical"
     * a measured statement rather than a hopeful one.
     */
    [Fact]
    public void TheWorldCacheCannotHoldAClusterListAtAll()
    {
        string path = _pathOf();
        if (File.Exists(path)) File.Delete(path);

        try
        {
            var storage = new DBStorage();
            var generated = _generate();

            Assert.True(storage.StoreCollection(DbName, generated));

            using (var raw = new LiteDB.LiteDatabase(path, storage.Mapper))
            {
                Assert.Equal(70, raw.GetCollection("ClusterDesc").Count());
            }

            Assert.False(storage.LoadCollection<ClusterDesc>(DbName, out _),
                "ClusterDesc round-trips now. That is a good thing and this test is why it "
                + "is visible: the cluster list would then genuinely be cached, and the "
                + "DbVersion bump would genuinely destroy it.");

            using (var raw = new LiteDB.LiteDatabase(path, storage.Mapper))
            {
                Assert.Empty(raw.GetCollectionNames());
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }


    /**
     * ⚠️ THE BUMP: a world cache written at 1039 is DELETED when 1040 opens it.
     *
     * Driven through DBStorage rather than through a description of it, because "deletes
     * the file" is a property of _open and not of anything this test could restate. The
     * content is a marker rather than a cluster list, so that what is being measured is the
     * deletion and not the defect above.
     */
    [Fact]
    public void TheBumpDeletesEverythingInTheWorldCache()
    {
        string path = _pathOf();
        if (File.Exists(path)) File.Delete(path);

        try
        {
            var storage = new DBStorage();

            Assert.True(storage.WithOpen(DbName, ClusterStorage.DbVersion - 1, db =>
            {
                db.GetCollection<Marker>().Insert(new Marker { Id = 1, Name = "world" });
                db.Commit();
            }));

            Assert.True(storage.WithOpen(DbName, ClusterStorage.DbVersion - 1, db =>
            {
                Assert.Equal(1, db.GetCollection<Marker>().Count());
            }));

            /*
             * ...and the bump, which is exactly what ClusterStorage.TryLoadClusterStreets
             * does on the first start after the version changed.
             */
            Assert.True(storage.WithOpen(DbName, ClusterStorage.DbVersion, db =>
            {
                Assert.Equal(0, db.GetCollection<Marker>().Count());
                Assert.Equal(ClusterStorage.DbVersion, db.UserVersion);
            }));

            /*
             * And the world that comes back afterwards is the same world.
             */
            _assertSame(_generate(), _generate());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }


    /**
     * A cache at the CURRENT version is not deleted.
     *
     * The control. Without it the test above passes for a DBStorage that deletes every file
     * it opens, which would be a far worse defect than the one being checked.
     */
    [Fact]
    public void ACacheAtTheCurrentVersionSurvivesBeingOpened()
    {
        string path = _pathOf();
        if (File.Exists(path)) File.Delete(path);

        try
        {
            var storage = new DBStorage();

            Assert.True(storage.WithOpen(DbName, ClusterStorage.DbVersion, db =>
            {
                db.GetCollection<Marker>().Insert(new Marker { Id = 1, Name = "world" });
                db.Commit();
            }));

            Assert.True(storage.WithOpen(DbName, ClusterStorage.DbVersion, db =>
            {
                Assert.Equal(1, db.GetCollection<Marker>().Count());
                Assert.Equal("world", db.GetCollection<Marker>().FindById(1).Name);
            }));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }


    /**
     * The version this phase ships at.
     *
     * Recorded so that the bump is a deliberate act with a number attached rather than
     * something that drifts: 1039 was the ground-only network, and 1040 is the first
     * version whose stored strokes may carry a structure Kind and a deck Level.
     */
    [Fact]
    public void TheWorldCacheVersionIsTheOneWpB6Bumped()
    {
        Assert.Equal(1040, ClusterStorage.DbVersion);
    }
}
