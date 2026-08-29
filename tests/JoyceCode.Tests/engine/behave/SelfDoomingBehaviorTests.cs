using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace JoyceCode.Tests.engine.behave;

/**
 * A behaviour that dooms the very entity it is ticking, from inside its own Behave,
 * has to remember that it did so.
 *
 * Dooming is not destroying: engine.Engine drains its doomed set later in the frame
 * and skips that drain outright on every eighth frame and on any frame whose budget
 * is spent, so the entity is still alive and still being ticked on the next frame.
 * builtin.tools.AutoRemoveBehavior was the one such behaviour in the tree with no
 * latch, and it crashed the game with "already was doomed before" from the quest
 * completion toast. CubeVanishBehavior, PolytopeVanishBehaviour and
 * FollowQuestToastBehavior all carried one; nothing said they had to.
 *
 * DoomedEntitySet is idempotent now, so this can no longer crash - but a behaviour
 * without the latch still re-dooms on every frame for the rest of its life, and the
 * next one written this way is the one that finds whatever the next consequence is.
 * This scans the source rather than running the behaviours because reaching Behave
 * needs I.Get&lt;engine.Engine&gt;(), and constructing an Engine registers about fifteen
 * services into the process-global I container.
 */
public class SelfDoomingBehaviorTests
{
    private static string _repoRoot()
    {
        string root = global::engine.GameRoot.PathTo("JoyceCode");
        Assert.False(String.IsNullOrEmpty(root), "could not locate the checkout");

        return Path.GetFullPath(Path.Combine(root, ".."));
    }


    /**
     * Return the source of every Behave(...) body in the file, brace matched.
     */
    private static IEnumerable<string> _behaveBodies(string source)
    {
        int at = 0;
        while (true)
        {
            int idxBehave = source.IndexOf("void Behave(", at, StringComparison.Ordinal);
            if (idxBehave < 0)
            {
                yield break;
            }

            int idxOpen = source.IndexOf('{', idxBehave);
            if (idxOpen < 0)
            {
                yield break;
            }

            int depth = 0;
            int idx = idxOpen;
            for (; idx < source.Length; ++idx)
            {
                if ('{' == source[idx]) ++depth;
                else if ('}' == source[idx])
                {
                    --depth;
                    if (0 == depth) break;
                }
            }

            yield return source.Substring(idxOpen, Math.Min(idx, source.Length - 1) - idxOpen + 1);
            at = idx;
        }
    }


    [Fact]
    public void EveryBehaviourThatDoomsItsOwnEntityLatchesThat()
    {
        string root = _repoRoot();

        List<string> offenders = new();
        List<string> checkedFiles = new();

        foreach (string path in new[] { "JoyceCode", "nogameCode" }
                     .SelectMany(dir => Directory.EnumerateFiles(
                         Path.Combine(root, dir), "*.cs", SearchOption.AllDirectories))
                     .OrderBy(f => f, StringComparer.Ordinal))
        {
            string source = File.ReadAllText(path);
            if (!source.Contains("AddDoomedEntity(entity)"))
            {
                continue;
            }

            if (!_behaveBodies(source).Any(body => body.Contains("AddDoomedEntity(entity)")))
            {
                /*
                 * Dooms some entity it was handed elsewhere - a collision partner, a
                 * child - which is a different situation and not repeated per frame.
                 */
                continue;
            }

            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            checkedFiles.Add(relative);

            /*
             * A bool field of its own is the latch every such behaviour here uses.
             * Nothing else in a behaviour survives across frames.
             */
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    source, @"private\s+bool\s+_\w+\s*=\s*false\s*;"))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(checkedFiles.Count >= 4,
            $"expected to find the known self-dooming behaviours, found {checkedFiles.Count}: "
            + String.Join(", ", checkedFiles));

        Assert.True(0 == offenders.Count,
            "These behaviours doom their own entity from Behave without remembering it, so "
            + "they doom it again on every frame until the engine gets around to draining "
            + "its doomed set. Add a 'private bool _isDoomed = false;' latch and return "
            + "early once it is set: "
            + String.Join(", ", offenders));
    }
}
