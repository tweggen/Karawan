using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace engine.casette;

public sealed class View
{
    // Event model for subscribers
    public enum DomChangeKind { Added, Removed, Modified }

    public sealed class DomChangeEvent
    {
        public string Path { get; }
        public DomChangeKind Kind { get; }
        public JsonNode? NewNode { get; }
        public JsonNode? OldNode { get; }
        public DateTimeOffset Timestamp { get; }

        public DomChangeEvent(string path, DomChangeKind kind, JsonNode? newNode, JsonNode? oldNode)
        {
            Path = path;
            Kind = kind;
            NewNode = newNode;
            OldNode = oldNode;
            Timestamp = DateTimeOffset.UtcNow;
        }
    }

    // Internal structure for partials (immutable payload)
    private sealed class PartialTree
    {
        public string Path { get; }
        public int Priority { get; }              // Lower number applied first; higher overrides later
        public long Version { get; }              // Monotonic per upsert
        public JsonDocument Payload { get; }      // Owns memory; stable beyond caller document lifetime

        public PartialTree(string path, int priority, long version, JsonDocument payload)
        {
            Path = path;
            Priority = priority;
            Version = version;
            Payload = payload;
        }
    }

    // Cache entry for merged subtree
    private sealed class CacheEntry
    {
        public long AggregateVersion { get; set; } // Combined version signature for fast equality
        public JsonNode? Node { get; set; }        // Shared reference to merged subtree
    }

    // Indexes
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly Dictionary<string, SortedSet<PartialTree>> _partialsByPath = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CacheEntry> _mergeCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Action<DomChangeEvent>>> _subscribers = new(StringComparer.Ordinal);

    private long _nextVersion = 1;

    // Custom sort: by priority asc, then version asc (older first), then path asc for tie‑break
    private sealed class PartialTreeComparer : IComparer<PartialTree>
    {
        public int Compare(PartialTree? x, PartialTree? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            int c = x.Priority.CompareTo(y.Priority);
            if (c != 0) return c;
            c = x.Version.CompareTo(y.Version);
            if (c != 0) return c;
            return string.Compare(x.Path, y.Path, StringComparison.Ordinal);
        }
    }

    private static readonly PartialTreeComparer PartialComparer = new();

    // Public API

    // Upsert a partial tree at an absolute path (JSON Pointer‑like, e.g. "/settings/theme").
    // Priority controls overlay order: higher priority wins.
    public void Upsert(string path, JsonElement element, int priority = 0)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/')
            throw new ArgumentException("Path must start with '/'", nameof(path));

        // Clone element to a private JsonDocument to ensure immutability and lifetime safety.
        var doc = JsonDocument.Parse(element.GetRawText());
        var pt = new PartialTree(path, priority, Interlocked.Increment(ref _nextVersion), doc);

        _lock.EnterWriteLock();
        JsonNode? oldMerged = null;
        JsonNode? newMerged = null;
        try
        {
            if (!_partialsByPath.TryGetValue(path, out var set))
            {
                set = new SortedSet<PartialTree>(PartialComparer);
                _partialsByPath[path] = set;
            }

            // Replace any existing partial with same path and priority by version ordering:
            // Keep multiples if needed; typically one per path. Here, remove older equal priority entries.
            var toRemove = set.Where(x => x.Priority == priority).ToList();
            foreach (var r in toRemove) set.Remove(r);
            set.Add(pt);

            // Invalidate caches affected by this path
            InvalidateCachesForPath(path);

            // Emit change events for subscribers of this path and ancestors
            oldMerged = GetMergedSubtreeNoLock(path); // merged after invalidation but before recompute
            newMerged = GetMergedSubtreeNoLock(path); // recompute will occur lazily in GetMergedSubtreeNoLock

            Publish(path, DetermineKind(oldMerged, newMerged), newMerged, oldMerged);
            foreach (var ancestor in EnumerateAncestors(path))
                Publish(ancestor, DomChangeKind.Modified, PeekCache(ancestor), PeekCache(ancestor));
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // Remove partials at a path (optional by priority). Triggers appropriate events.
    public void Remove(string path, int? priority = null)
    {
        _lock.EnterWriteLock();
        JsonNode? oldMerged = null;
        JsonNode? newMerged = null;
        try
        {
            if (_partialsByPath.TryGetValue(path, out var set))
            {
                if (priority is null)
                    set.Clear();
                else
                    foreach (var r in set.Where(x => x.Priority == priority.Value).ToList())
                        set.Remove(r);

                if (set.Count == 0) _partialsByPath.Remove(path);

                InvalidateCachesForPath(path);

                oldMerged = GetMergedSubtreeNoLock(path);
                newMerged = GetMergedSubtreeNoLock(path);

                Publish(path, DetermineKind(oldMerged, newMerged), newMerged, oldMerged);
                foreach (var ancestor in EnumerateAncestors(path))
                    Publish(ancestor, DomChangeKind.Modified, PeekCache(ancestor), PeekCache(ancestor));
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    // Subscribe to changes at a path (exact path). Returns an unsubscribe handle.
    public IDisposable Subscribe(string path, Action<DomChangeEvent> handler)
    {
        if (string.IsNullOrEmpty(path) || path[0] != '/')
            throw new ArgumentException("Path must start with '/'", nameof(path));
        if (handler is null) throw new ArgumentNullException(nameof(handler));

        _lock.EnterWriteLock();
        try
        {
            if (!_subscribers.TryGetValue(path, out var list))
            {
                list = new List<Action<DomChangeEvent>>();
                _subscribers[path] = list;
            }
            list.Add(handler);
        }
        finally
        {
            _lock.ExitWriteLock();
        }

        return new Subscription(this, path, handler);
    }

    // Get a merged subtree reference at a path. Returns a stable JsonNode reference from cache.
    public JsonNode? GetMergedSubtree(string path)
    {
        _lock.EnterUpgradeableReadLock();
        try
        {
            if (_mergeCache.TryGetValue(path, out var entry))
            {
                return entry.Node;
            }

            _lock.EnterWriteLock();
            try
            {
                // Double‑check in case another writer filled it
                if (_mergeCache.TryGetValue(path, out var existing))
                    return existing.Node;

                var node = ComputeMergedSubtree(path, out var aggVersion);
                _mergeCache[path] = new CacheEntry { AggregateVersion = aggVersion, Node = node };
                return node;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        finally
        {
            _lock.ExitUpgradeableReadLock();
        }
    }

    // Internal helpers

    private sealed class Subscription : IDisposable
    {
        private readonly View _owner;
        private readonly string _path;
        private readonly Action<DomChangeEvent> _handler;
        private int _disposed;

        public Subscription(View owner, string path, Action<DomChangeEvent> handler)
        {
            _owner = owner;
            _path = path;
            _handler = handler;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) return;
            _owner._lock.EnterWriteLock();
            try
            {
                if (_owner._subscribers.TryGetValue(_path, out var list))
                {
                    list.RemoveAll(h => h == _handler);
                    if (list.Count == 0) _owner._subscribers.Remove(_path);
                }
            }
            finally
            {
                _owner._lock.ExitWriteLock();
            }
        }
    }

    private void Publish(string path, DomChangeKind kind, JsonNode? newNode, JsonNode? oldNode)
    {
        if (!_subscribers.TryGetValue(path, out var list) || list.Count == 0) return;
        var evt = new DomChangeEvent(path, kind, newNode, oldNode);
        // Publish outside locks to avoid reentrancy risks
        foreach (var h in list.ToArray())
        {
            try { h(evt); }
            catch { /* swallow to keep bus alive */ }
        }
    }

    private static DomChangeKind DetermineKind(JsonNode? oldNode, JsonNode? newNode)
    {
        if (oldNode is null && newNode is not null) return DomChangeKind.Added;
        if (oldNode is not null && newNode is null) return DomChangeKind.Removed;
        return DomChangeKind.Modified;
    }

    private JsonNode? PeekCache(string path)
    {
        return _mergeCache.TryGetValue(path, out var entry) ? entry.Node : null;
    }

    private IEnumerable<string> EnumerateAncestors(string path)
    {
        if (path == "/") yield break;
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var acc = "";
        for (int i = 0; i < parts.Length - 1; i++)
        {
            acc += "/" + parts[i];
            yield return acc;
        }
    }

    private void InvalidateCachesForPath(string path)
    {
        // Invalidate exactly the path and all descendant paths
        var toInvalidate = _mergeCache.Keys.Where(k => IsAncestorOrEqual(path, k)).ToList();
        foreach (var k in toInvalidate)
            _mergeCache.Remove(k);
    }

    private static bool IsAncestorOrEqual(string ancestor, string descendant)
    {
        if (ancestor == descendant) return true;
        if (descendant.Length < ancestor.Length) return false;
        if (!descendant.StartsWith(ancestor, StringComparison.Ordinal)) return false;
        // Ensure boundary on segment ("/a" is not ancestor of "/ab")
        if (descendant.Length == ancestor.Length) return true;
        return descendant[ancestor.Length] == '/';
    }

    private JsonNode? GetMergedSubtreeNoLock(string path)
    {
        if (_mergeCache.TryGetValue(path, out var existing))
            return existing.Node;

        var node = ComputeMergedSubtree(path, out var aggVersion);
        _mergeCache[path] = new CacheEntry { AggregateVersion = aggVersion, Node = node };
        return node;
    }

    private JsonNode? ComputeMergedSubtree(string path, out long aggregateVersion)
    {
        aggregateVersion = 0;

        // Collect all partials that are equal to target path or descend into it.
        // Strategy:
        // - Partials at exactly 'path': overlay their payload directly.
        // - Partials under 'path/…': insert payload at their relative subpath below 'path'.
        // Overlay order: priority asc, then version asc; later writes override earlier.
        var affecting = new List<PartialTree>();

        foreach (var kvp in _partialsByPath)
        {
            var p = kvp.Key;
            if (p == path || IsAncestorOrEqual(path, p))
                affecting.AddRange(kvp.Value);
        }

        if (affecting.Count == 0)
            return null;

        // Build target node incrementally
        JsonNode? root = null;

        foreach (var pt in affecting.OrderBy(x => x, PartialComparer))
        {
            aggregateVersion = unchecked(aggregateVersion + pt.Version * 17 + pt.Priority * 131);

            var payloadNode = JsonNode.Parse(pt.Payload.RootElement.GetRawText());
            if (pt.Path == path)
            {
                // Overlay whole payload onto root
                root = MergeOverlay(root, payloadNode);
            }
            else
            {
                // Insert payload at relative path segment under root
                var relSegments = GetRelativeSegments(path, pt.Path);
                root ??= new JsonObject();
                EnsurePath(root, relSegments) // returns the parent node where payload should be merged
                    .AssignOverlay(payloadNode);
            }
        }

        return root;
    }

    private static IEnumerable<string> GetRelativeSegments(string basePath, string targetPath)
    {
        // basePath "/a/b", target "/a/b/c/d" => ["c","d"]
        var baseParts = basePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var targetParts = targetPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = baseParts.Length; i < targetParts.Length; i++)
            yield return targetParts[i];
    }

    private static JsonNode MergeOverlay(JsonNode? target, JsonNode? overlay)
    {
        if (overlay is null) return target ?? new JsonObject();

        if (target is null)
            return overlay.DeepClone();

        if (overlay is JsonValue)
            return overlay.DeepClone(); // value override

        if (overlay is JsonArray arrOverlay)
        {
            // Replace arrays (simpler and faster). Customize to merge by index if needed.
            return arrOverlay.DeepClone();
        }

        if (overlay is JsonObject objOverlay)
        {
            var result = target as JsonObject ?? new JsonObject();
            foreach (var kv in objOverlay)
            {
                var key = kv.Key;
                var ov = kv.Value;
                if (result[key] is null)
                {
                    result[key] = ov?.DeepClone();
                }
                else
                {
                    result[key] = MergeOverlay(result[key], ov);
                }
            }
            return result;
        }

        return target;
    }

    // Ensure path exists (creating JsonObjects along the way) and return the leaf node reference.
    private static JsonObject EnsurePath(JsonNode root, IEnumerable<string> segments)
    {
        var obj = root as JsonObject ?? throw new InvalidOperationException("Root must be an object");
        foreach (var seg in segments)
        {
            var next = obj[seg];
            if (next is null)
            {
                var created = new JsonObject();
                obj[seg] = created;
                obj = created;
            }
            else if (next is JsonObject o)
            {
                obj = o;
            }
            else
            {
                // Replace non‑object with object (to allow subtree merging)
                var created = new JsonObject();
                obj[seg] = created;
                obj = created;
            }
        }
        return obj;
    }
}

// Extension to assign overlay conveniently to a JsonObject leaf
internal static class JsonNodeExtensions
{
    public static void AssignOverlay(this JsonObject obj, JsonNode? overlay)
    {
        if (overlay is null) return;
        var objOverlay = overlay as JsonObject;
        if (null != objOverlay)
        {
            foreach (var kv in objOverlay)
            {
                obj[kv.Key] = kv.Value?.DeepClone();
            }
        }
    }
}
