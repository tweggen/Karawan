using System;

namespace engine.tale;

/// <summary>
/// Which narration script a TALE NPC talks with (Phase C2). Pure: whether a
/// script exists is asked through <c>scriptExists</c>, so the order can be
/// tested without a NarrationManager (KAR-433).
/// </summary>
public static class ConversationScriptResolver
{
    public const string Generic = "tale.generic";


    /// <summary>
    /// Five levels, first hit wins:
    /// 1. storylet.ConversationScript (explicit override, not checked for existence);
    /// 2. tale.{storyletId};
    /// 3. the first tag with a tale.tag.{tag};
    /// 4. tale.role.{role};
    /// 5. tale.generic.
    /// </summary>
    public static string Resolve(StoryletDefinition storylet, string role, Func<string, bool> scriptExists)
    {
        if (storylet == null) return Generic;

        if (!string.IsNullOrEmpty(storylet.ConversationScript)) return storylet.ConversationScript;

        string idScript = $"tale.{storylet.Id}";
        if (scriptExists(idScript)) return idScript;

        if (storylet.Tags != null)
        {
            foreach (var tag in storylet.Tags)
            {
                string tagScript = $"tale.tag.{tag}";
                if (scriptExists(tagScript)) return tagScript;
            }
        }

        if (!string.IsNullOrEmpty(role))
        {
            string roleScript = $"tale.role.{role}";
            if (scriptExists(roleScript)) return roleScript;
        }

        return Generic;
    }
}
