using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace JoyceCode.Tests.nogame;

/**
 * A per-entity event subscription is taken back with the path it was made on (KAR-453).
 *
 * FleeStrategy subscribed _onHitEvent on EntityStrategy.HitEventPath in OnEnter and
 * unsubscribed it from CrashEventPath in OnExit. The unsubscribe silently did nothing, so
 * every flee left a handler on the entity's hit path: a later hit reached a FleeStrategy
 * that was no longer active, re-armed its timer and touched its Navigator, and the handlers
 * piled up per flee.
 *
 * The strategies cannot be compiled here (nogame is not referenced), so this scans the
 * source: in every file, each Subscribe(X.SomethingEventPath(...), handler) must have an
 * Unsubscribe(X.SomethingEventPath(...), handler) with the same path method and handler.
 * The entity argument is not compared - OnAttach and OnDetach name it differently.
 */
public class EntityEventPathSubscriptionTests
{
    private static readonly Regex _call = new(
        @"(?<!\w)(?<verb>Subscribe|Unsubscribe)\(\s*(?<path>[\w.]*EventPath)\s*\([^()]*\)\s*,\s*(?<handler>[\w.]+)\s*[,)]",
        RegexOptions.Compiled);


    private static string _stripComments(string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", " ", RegexOptions.Singleline);
        return Regex.Replace(source, @"//[^\n]*", " ");
    }


    /**
     * Subscriptions in the source that have no unsubscribe with the same path method and
     * handler, as "path, handler".
     */
    public static List<string> Unmatched(string source)
    {
        var subscribed = new List<string>();
        var unsubscribed = new HashSet<string>();
        foreach (Match m in _call.Matches(_stripComments(source)))
        {
            string key = $"{m.Groups["path"].Value}, {m.Groups["handler"].Value}";
            if (m.Groups["verb"].Value == "Subscribe") subscribed.Add(key);
            else unsubscribed.Add(key);
        }

        return subscribed.Where(k => !unsubscribed.Contains(k)).ToList();
    }


    private static IEnumerable<string> _sourceFiles()
    {
        string joyceCode = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(joyceCode), "could not locate the checkout");
        string nogameCode = Path.GetFullPath(Path.Combine(joyceCode, "..", "nogameCode"));
        Assert.True(Directory.Exists(nogameCode), $"could not find nogameCode at {nogameCode}");

        return Directory.EnumerateFiles(joyceCode, "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(nogameCode, "*.cs", SearchOption.AllDirectories));
    }


    [Fact]
    public void EveryEventPathSubscriptionIsUnsubscribedFromTheSamePath()
    {
        var problems = new List<string>();
        int seen = 0;
        foreach (var file in _sourceFiles())
        {
            string source = File.ReadAllText(file);
            seen += _call.Matches(_stripComments(source)).Count(m => m.Groups["verb"].Value == "Subscribe");
            foreach (var key in Unmatched(source))
            {
                problems.Add($"{Path.GetFileName(file)}: Subscribe({key}) has no matching Unsubscribe");
            }
        }

        /*
         * Citizen Entity/Flee/Recover/TaleEntity strategies, the two walk behaviours and
         * niceday's RestStrategy: if the scan finds far fewer, the regex has stopped matching
         * and this test would pass on nothing.
         */
        Assert.True(seen >= 9, $"only {seen} EventPath subscriptions found; the scan is broken");
        Assert.Empty(problems);
    }


    [Fact]
    public void TheFleeStrategyBugIsCaught()
    {
        const string before = @"
            public override void OnExit()
            {
                var sm = I.Get<SubscriptionManager>();
                sm.Unsubscribe(EntityStrategy.CrashEventPath(_entity), _onHitEvent);
            }
            public override void OnEnter()
            {
                var sm = I.Get<SubscriptionManager>();
                sm.Subscribe(EntityStrategy.HitEventPath(_entity), _onHitEvent);
            }";
        Assert.Equal(new[] { "EntityStrategy.HitEventPath, _onHitEvent" }, Unmatched(before));
        Assert.Empty(Unmatched(before.Replace("CrashEventPath", "HitEventPath")));
    }


    [Fact]
    public void TheEntityArgumentMayDifferAndCallsMaySpanLines()
    {
        const string source = @"
            I.Get<SubscriptionManager>().Subscribe(
                EntityStrategy.BumpEventPath(entity0), _onBumpEvent);
            // sm.Unsubscribe(EntityStrategy.BumpEventPath(entity), _onBumpEvent); in a comment does not count
            I.Get<SubscriptionManager>().Unsubscribe(
                EntityStrategy.BumpEventPath(entity), _onBumpEvent);";
        Assert.Empty(Unmatched(source));
        Assert.Single(Unmatched(source.Substring(0, source.IndexOf("// sm.", StringComparison.Ordinal))));
    }
}
