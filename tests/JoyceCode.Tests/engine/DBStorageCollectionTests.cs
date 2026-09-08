using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using engine;
using engine.world;
using LiteDB;
using Xunit;

namespace JoyceCode.Tests.engine;


/**
 * ⚠️ engine.Logger.SetLogTarget is a PROCESS GLOBAL, and every property in this file is
 * observable only as a log line, so a concurrently running class that installs its own
 * target would take these lines away and the presence assertions would fail at random.
 *
 * §14's recorded flake is the same hazard seen from the other side (a class asserting a
 * line is ABSENT received a concurrent class's lines). Its remedy - keep only what was
 * written on the capturing thread - is applied below too, but it is not sufficient here:
 * filtering what comes IN does not stop somebody else calling SetLogTarget and taking the
 * target away, and Logger offers no way to read the installed target back and notice.
 *
 * So this collection runs alone. DisableParallelization on a CollectionDefinition means
 * this collection does not run in parallel with any OTHER collection either, which is the
 * whole point - the "assimp" collection beside it uses the same mechanism for the same
 * reason, a process-global that cannot be shared.
 *
 * The structural fix, for whoever gets there: one shared collection joined by every class
 * that installs a log target (today StructureHeightTests, GradeConvergenceTests and
 * StructurePlacementTests, all under engine/streets/, all owned by concurrent work at the
 * time this was written).
 */
[CollectionDefinition("logtarget", DisableParallelization = true)]
public class LogTargetCollection
{
}


/**
 * WP-B6 §15.6's "found and NOT fixed": DBStorage._readCollection drops a collection it
 * cannot read and says nothing at all about it, not even a Trace.
 *
 * ⚠️ WHAT IT HIDES, and this is the whole reason the file exists: ClusterDesc serialises
 * its StreetHeightSource and FlatStreetHeight has no parameterless constructor, so LiteDB
 * writes the seventy cities of the shipped world into the cache happily and throws on the
 * way back. The inner catch then deletes them. Every start of the game has done that,
 * silently, for as long as the cache has existed - it came to light only because somebody
 * was verifying something else. That is the suppressed-Warning lesson again: two rounds of
 * a live investigation once went by with "there are no logs" taken as evidence that
 * nothing was wrong.
 *
 * ⚠️ AND THE OUTER CATCH WAS WORSE THAN SILENT. It read `c.GetType()` on the out
 * parameter, which is null on every path that can reach it, so the handler raised a
 * NullReferenceException of its own, threw the real exception away and propagated out of
 * _readCollection instead of reporting anything.
 *
 * Every gate here is on the log LINE, because that is where the property lives. A source
 * scan would be satisfied by the identifier surviving in a comment - §7m's lesson, "a
 * driver is a call, not a mention" - and the two silences above are precisely the shape of
 * code that looks right and emits nothing.
 */
[Collection("logtarget")]
public class DBStorageCollectionTests
{
    private const string DbName = "dbstorage-report-probe";

    /**
     * Error(Dc, ...) prefixes the category. Built by concatenation because "global::" in
     * an interpolation hole is read as a format specifier.
     */
    private static readonly string _dcTag = "[" + global::engine.Dc.Database + "]";


    public class Good
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }


    private static string _pathOf(string dbName)
        => Path.Combine(GlobalSettings.Get("Engine.RWPath") ?? "", dbName + ".db");


    private static void _clean(string dbName)
    {
        string p = _pathOf(dbName);
        if (File.Exists(p)) File.Delete(p);
    }


    /**
     * A log target that keeps what THIS TEST wrote to it.
     *
     * The thread filter is §14's: a test body runs on one thread and everything driven
     * here is synchronous on it, so the capturing thread's id is exactly the filter that
     * is wanted. The collection above is what stops the target being taken away.
     */
    private sealed class LogCapture : global::engine.ILogTarget, IDisposable
    {
        private readonly List<string> _lines = new();
        private readonly object _lo = new();
        private readonly int _threadId = Environment.CurrentManagedThreadId;

        internal LogCapture() => global::engine.Logger.SetLogTarget(this);

        public void AddLogEntry(in global::engine.Logger.Level level, in string logEntry)
        {
            if (Environment.CurrentManagedThreadId != _threadId)
            {
                return;
            }

            lock (_lo)
            {
                _lines.Add($"{level}|{logEntry}");
            }
        }

        internal IReadOnlyList<string> Lines
        {
            get { lock (_lo) { return _lines.ToList(); } }
        }

        internal IReadOnlyList<string> At(global::engine.Logger.Level level)
        {
            lock (_lo)
            {
                return _lines.Where(l => l.StartsWith($"{level}|", StringComparison.Ordinal)).ToList();
            }
        }

        internal string TheOne(global::engine.Logger.Level level, string mustContain)
        {
            var candidates = At(level).Where(
                l => l.Contains(mustContain, StringComparison.Ordinal)).ToList();
            Assert.True(1 == candidates.Count,
                $"expected exactly one {level} line containing \"{mustContain}\", saw "
                + $"{candidates.Count}; all captured lines were:\n  "
                + string.Join("\n  ", Lines));

            return candidates[0];
        }

        public void Dispose() => global::engine.Logger.SetLogTarget(null);
    }


    private static List<ClusterDesc> _shippedClusterList()
    {
        var op = new GenerateClustersOperator("mydear");
        op._generateClusterList(null, out var list);

        return new List<ClusterDesc>(list);
    }


    /**
     * ⚠️ THE DEFECT, DRIVEN END TO END ON THE REAL THING.
     *
     * Not a synthetic type that throws on demand: the seventy cities of the shipped world,
     * stored through the shipped DBStorage and read back through it. The cache genuinely
     * cannot hold them, the collection genuinely goes away, and the point of this test is
     * that the game now SAYS SO.
     *
     * The line has to carry three things, and each is asserted separately because each was
     * separately absent: WHICH type could not be read (a log line naming no type sends the
     * reader nowhere), that data is being DISCARDED rather than merely not returned, and
     * the underlying exception - here LiteDB naming FlatStreetHeight, which is the actual
     * repair anybody would make.
     *
     * It is an Error and not a Trace: a category filter decides how much detail to keep,
     * and seventy cities being deleted is not detail. It is not a Warning either - nothing
     * about this is recoverable-and-fine; a cache that cannot read back what it just wrote
     * is broken.
     */
    [Fact]
    public void AnUnreadableCollectionSaysWhatItIsThrowingAway()
    {
        _clean(DbName);
        try
        {
            var storage = new DBStorage();

            /*
             * Generated OUTSIDE the capture window on purpose: building the cluster list
             * emits a Warning of its own about DisableClusterFlattening, and a control
             * elsewhere in this file asserts that a healthy read says nothing at all.
             */
            var clusters = _shippedClusterList();
            Assert.Equal(70, clusters.Count);
            Assert.True(storage.StoreCollection(DbName, clusters));

            using (var db = new LiteDatabase(_pathOf(DbName), storage.Mapper))
            {
                Assert.Equal(70, db.GetCollection("ClusterDesc").Count());
            }

            string line;
            using (var cap = new LogCapture())
            {
                Assert.False(storage.LoadCollection<ClusterDesc>(DbName, out _),
                    "ClusterDesc round-trips now. That is a good thing, and this test is "
                    + "how it becomes visible rather than a surprise - see "
                    + "ClusterCacheVersionTests.TheWorldCacheCannotHoldAClusterListAtAll, "
                    + "which says what starts mattering the day it does.");

                line = cap.TheOne(global::engine.Logger.Level.Error, "DISCARDING");
            }

            Assert.Contains("engine.world.ClusterDesc", line, StringComparison.Ordinal);
            Assert.Contains("DISCARDING the 70 stored document(s)", line, StringComparison.Ordinal);
            Assert.Contains("FlatStreetHeight", line, StringComparison.Ordinal);
            Assert.Contains(_dcTag, line, StringComparison.Ordinal);

            /*
             * And it really is gone - the report is about something that happened.
             */
            using (var db = new LiteDatabase(_pathOf(DbName), storage.Mapper))
            {
                Assert.Empty(db.GetCollectionNames());
            }
        }
        finally
        {
            _clean(DbName);
        }
    }


    /**
     * ⚠️ THE REPORT COMES FIRST, AND THE ORDER IS LOAD BEARING.
     *
     * DropCollection and Commit can throw - measured, they do on a database that has been
     * disposed - and if they throw after the cause has been reported the outer catch adds
     * a second line beside a first that already says why the drop was attempted. Report
     * afterwards instead and the original exception is lost behind the failure to act on
     * it, which is a worse silence than the one this whole change removes, because it
     * looks like a different bug.
     *
     * Asserted on the source because it is an ordering within one handler and there is no
     * behaviour that distinguishes the two orders on a database where the drop succeeds -
     * which is every database this could otherwise be driven on.
     */
    [Fact]
    public void TheCauseIsReportedBeforeTheCollectionIsDropped()
    {
        string source = File.ReadAllText(Path.Combine(
            _repoRoot(), "JoyceCode", "engine", "DBStorage.cs"));

        int report = source.IndexOf("DISCARDING the ", StringComparison.Ordinal);
        int drop = source.IndexOf("db.DropCollection(typeof(ObjType).Name)", StringComparison.Ordinal);

        Assert.True(report >= 0, "the drop is not reported at all any more");
        Assert.True(drop >= 0, "the drop moved; this scan no longer measures anything");
        Assert.True(report < drop,
            "the collection is dropped before the reason for dropping it is reported, so a "
            + "throw from DropCollection/Commit loses the cause entirely");
    }


    /**
     * ⚠️ THE OUTER HANDLER REPORTS INSTEAD OF THROWING FROM INSIDE ITSELF.
     *
     * It used to read c.GetType(), and `c` is the out parameter - null on entry, assigned
     * only on the success path inside the inner try, so null on every path that reaches
     * here. Measured before the fix: NullReferenceException at DBStorage.cs:152, the real
     * exception discarded, nothing logged, and the failure propagating out of a method
     * whose whole contract is to return false.
     *
     * ⚠️ NO LIVE-DATABASE ROUTE TO THIS HANDLER WAS FOUND, and that is said out loud
     * rather than implied by a green test. GetCollection<T> and Count() were driven with a
     * generic type, a type with no parameterless constructor and a collection name LiteDB
     * should not accept, and all of them succeeded; the only throws found are on a
     * DISPOSED database. So the outer catch is a backstop, not a live path - kept, because
     * an IO or corruption fault inside Count() is entirely plausible and unlike §7q's
     * unreachable fallback it cannot be shown impossible, and gated so that when it does
     * fire it reports rather than replacing the fault with one of its own.
     *
     * _readCollection is internal for this, the way BlockGraph.ChainsAreClear was: the
     * state that reaches this handler cannot be handed in through any public entry point,
     * since WithOpen owns the database's lifetime and the action only ever sees a live one.
     */
    [Fact]
    public void TheOuterHandlerReportsInsteadOfRaisingAnExceptionOfItsOwn()
    {
        const string probe = DbName + "-outer";
        _clean(probe);
        try
        {
            var storage = new DBStorage();

            var db = new LiteDatabase(_pathOf(probe), storage.Mapper);
            db.GetCollection<Good>().Insert(new Good { Id = 1, Name = "x" });
            db.Commit();
            db.Dispose();

            string line;
            bool haveIt;
            IEnumerable<Good> got;
            using (var cap = new LogCapture())
            {
                /*
                 * Count() on a disposed database throws ObjectDisposedException while
                 * GetCollection() succeeds, so this lands in the outer catch exactly.
                 */
                haveIt = storage._readCollection<Good>(db, null, out got);
                line = cap.TheOne(global::engine.Logger.Level.Error, "Unable to load collection");
            }

            Assert.False(haveIt);
            Assert.Null(got);
            Assert.Contains("JoyceCode.Tests.engine.DBStorageCollectionTests+Good", line,
                StringComparison.Ordinal);
            Assert.DoesNotContain("NullReferenceException", line, StringComparison.Ordinal);
            Assert.Contains("ObjectDisposedException", line, StringComparison.Ordinal);
            Assert.Contains(_dcTag, line, StringComparison.Ordinal);
        }
        finally
        {
            _clean(probe);
        }
    }


    /**
     * THE CONTROL: a collection that reads back says NOTHING.
     *
     * Without it every assertion above is satisfied by a DBStorage that reports a disaster
     * on every successful load, which would be a worse defect than the silence - a warning
     * that fires during normal operation is a warning nobody reads, which is how the
     * suppressed-Warning entry began.
     *
     * Asserted over every level at or above Warning rather than over one message, so it
     * cannot be satisfied by moving the noise to a different string.
     */
    [Fact]
    public void AHealthyCollectionRoundTripsAndSaysNothing()
    {
        const string probe = DbName + "-healthy";
        _clean(probe);
        try
        {
            var storage = new DBStorage();
            var written = new List<Good>
            {
                new Good { Id = 1, Name = "one" },
                new Good { Id = 2, Name = "two" },
            };

            IReadOnlyList<string> lines;
            bool haveIt;
            IEnumerable<Good> got;
            using (var cap = new LogCapture())
            {
                Assert.True(storage.StoreCollection(probe, written));
                haveIt = storage.LoadCollection<Good>(probe, out got);
                lines = cap.Lines;
            }

            Assert.True(haveIt);
            Assert.Equal(new[] { "one", "two" }, got.OrderBy(g => g.Id).Select(g => g.Name));

            var loud = lines.Where(
                l => l.StartsWith("Error|", StringComparison.Ordinal)
                     || l.StartsWith("Warning|", StringComparison.Ordinal)
                     || l.StartsWith("Fatal|", StringComparison.Ordinal)).ToList();
            Assert.True(0 == loud.Count,
                "a healthy round trip reported something:\n  " + string.Join("\n  ", loud));
        }
        finally
        {
            _clean(probe);
        }
    }


    /**
     * THE SECOND CONTROL: an ABSENT collection is not a failure.
     *
     * A first start has no cache, and both shipped callers regenerate happily. Reporting
     * that as an Error would put a line in front of every player on every fresh install
     * saying something had gone wrong, which is the same defect as the silence with its
     * sign flipped. It stays the Trace it always was.
     */
    [Fact]
    public void AnAbsentCollectionIsNotReportedAsAFailure()
    {
        const string probe = DbName + "-absent";
        _clean(probe);
        try
        {
            var storage = new DBStorage();

            IReadOnlyList<string> lines;
            bool haveIt;
            using (var cap = new LogCapture())
            {
                haveIt = storage.LoadCollection<Good>(probe, out _);
                lines = cap.Lines;
            }

            Assert.False(haveIt);

            var loud = lines.Where(
                l => l.StartsWith("Error|", StringComparison.Ordinal)
                     || l.StartsWith("Warning|", StringComparison.Ordinal)
                     || l.StartsWith("Fatal|", StringComparison.Ordinal)).ToList();
            Assert.True(0 == loud.Count,
                "an empty cache was reported as a failure:\n  " + string.Join("\n  ", loud));
        }
        finally
        {
            _clean(probe);
        }
    }


    /**
     * ⚠️ RECORDED AND DELIBERATELY NOT FIXED: a query we cannot translate DESTROYS data
     * that was perfectly readable.
     *
     * The inner try covers two different things - materialising the stored documents, and
     * translating the CALLER's predicate - and the catch cannot tell them apart, so it
     * applies the same repair to both. Measured here: two intact documents, a predicate
     * LiteDB cannot translate, and the collection is gone. Nothing about the data was
     * wrong; the fault was entirely on the calling side, and the recovery deleted the
     * player's data for it.
     *
     * Drop-and-report is the right recovery for the whole-collection read - both callers
     * regenerate on a false return, so a collection that cannot be read costs nothing to
     * discard, while keeping it means failing identically on every start for the life of
     * the install. It is NOT obviously right here, and keep-and-refuse probably is.
     *
     * Not changed, for two reasons. It is latent: both LoadCollection call sites in the
     * tree - GenerateClustersOperator._findClusters and nogame.config.Module._loadGameConfig
     * - use the no-predicate overload, and nothing anywhere passes a predicate. And
     * changing WHEN stored data is destroyed has every player's worldcache and gameconfig
     * behind it, which is a decision to hand over rather than to take in a round about
     * making failures visible.
     *
     * This test is what makes the change visible when somebody makes it: it fails the day
     * the data survives, and the message says that is the good outcome.
     */
    [Fact]
    public void AQueryThatCannotBeTranslatedStillDestroysTheStoredData()
    {
        const string probe = DbName + "-predicate";
        _clean(probe);
        try
        {
            var storage = new DBStorage();
            Assert.True(storage.StoreCollection(probe, new List<Good>
            {
                new Good { Id = 1, Name = "one" },
                new Good { Id = 2, Name = "two" },
            }));

            /*
             * A control first: a predicate LiteDB CAN translate reads back, so what the
             * test below measures is the translation failing and not predicates in general.
             */
            Assert.True(storage.LoadCollection<Good>(probe, g => g.Id == 1, out var one));
            Assert.Single(one);

            Func<Good, bool> opaque = g => g.Name.GetHashCode() > 0;

            string line;
            using (var cap = new LogCapture())
            {
                Assert.False(storage.LoadCollection<Good>(probe, g => opaque(g), out _));
                line = cap.TheOne(global::engine.Logger.Level.Error, "DISCARDING");
            }

            /*
             * ⚠️ The line says a predicate was in play, because that is exactly the
             * distinction a reader needs and cannot otherwise make: the same message with
             * no predicate means the stored data is bad, and with one it may equally mean
             * the query is.
             */
            Assert.Contains("with a predicate", line, StringComparison.Ordinal);
            Assert.Contains("DISCARDING the 2 stored document(s)", line, StringComparison.Ordinal);

            using (var db = new LiteDatabase(_pathOf(probe), storage.Mapper))
            {
                Assert.Empty(db.GetCollectionNames());
            }
        }
        finally
        {
            _clean(probe);
        }
    }


    /**
     * ⚠️ THE SAME DEFECT AS THE OUTER CATCH, THIRD INSTANCE, and this one was
     * unconditional: _writeCollection's null guard read c.GetType() in the branch that
     * runs exactly when c is null.
     *
     * So it raised a NullReferenceException before ErrorThrow was ever reached - the log
     * line never printed, and the caller got an NRE where the method's own contract says
     * ArgumentException. Both halves are asserted: the exception type that comes out, and
     * the line that finally says which collection was null.
     *
     * (WithOpen catches the throw from the action and reports it, which is why
     * StoreCollection still returns rather than propagating; the exception's identity is
     * read out of the line WithOpen writes.)
     */
    [Fact]
    public void StoringANullCollectionReportsWhichOneAndFailsAsAnArgumentException()
    {
        const string probe = DbName + "-null";
        _clean(probe);
        try
        {
            var storage = new DBStorage();

            string mine, wrapped;
            using (var cap = new LogCapture())
            {
                storage.StoreCollection<Good>(probe, null);

                /*
                 * Two Error lines, and the second QUOTES the first - ErrorThrow puts its
                 * own message into the exception, which WithOpen then prints. So the guard's
                 * own line is the one that is not WithOpen's.
                 */
                wrapped = cap.TheOne(global::engine.Logger.Level.Error, "Unable to execute action");
                var own = cap.At(global::engine.Logger.Level.Error).Where(
                    l => !l.Contains("Unable to execute action", StringComparison.Ordinal)).ToList();
                mine = Assert.Single(own);
            }

            Assert.Contains("JoyceCode.Tests.engine.DBStorageCollectionTests+Good", mine,
                StringComparison.Ordinal);
            Assert.Contains("ArgumentException", wrapped, StringComparison.Ordinal);
            Assert.DoesNotContain("NullReferenceException", wrapped, StringComparison.Ordinal);
        }
        finally
        {
            _clean(probe);
        }
    }


    /**
     * Neither database version is touched by this change, asserted rather than asserted in
     * prose.
     *
     * ⚠️ DBStorage.DbVersion also governs gamestate.db, so a bump here deletes every
     * player's SAVE, not just the world cache; ClusterStorage.DbVersion deletes the whole
     * worldcache file. Nothing about reporting a failure that was already happening changes
     * what is stored or how it is read, so both stay where WP-B6 left them.
     */
    [Fact]
    public void NeitherStoredFormatChanged()
    {
        Assert.Equal(1040, global::engine.streets.ClusterStorage.DbVersion);

        var f = typeof(DBStorage).GetField("DbVersion",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(f);
        Assert.Equal(3, (int)f.GetRawConstantValue());
    }


    private static string _repoRoot()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        return Path.GetFullPath(Path.Combine(root, ".."));
    }
}
