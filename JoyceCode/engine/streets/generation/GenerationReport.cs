using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace engine.streets.generation;


/**
 * Opt-in diagnostics for one generation run.
 *
 * Replaces the orphan-point tracking that WP-2c deleted. That machinery ran
 * unconditionally, maintained four collections per candidate, and reported on a
 * failure mode which had been impossible since candidates stopped being materialised
 * before they were accepted.
 *
 * This is off unless someone asks for it, costs a dictionary increment per rejection
 * when on, and answers the question that is actually useful when a cluster comes out
 * wrong: which constraint is throwing everything away.
 */
internal sealed class GenerationReport
{
    private readonly Dictionary<string, int> _rejectionsByReason = new();

    internal int Accepted;
    internal int Restarts;
    internal int Splits;
    internal int RestartBudgetExhausted;


    internal void CountRejection(string reason)
    {
        _rejectionsByReason.TryGetValue(reason ?? "unspecified", out int n);
        _rejectionsByReason[reason ?? "unspecified"] = n + 1;
    }


    internal string Describe()
    {
        var sb = new StringBuilder();
        sb.Append($"accepted {Accepted}, restarts {Restarts}, splits {Splits}");

        if (RestartBudgetExhausted > 0)
        {
            sb.Append($", RESTART BUDGET EXHAUSTED {RestartBudgetExhausted}x");
        }

        foreach (var kv in _rejectionsByReason.OrderByDescending(kv => kv.Value))
        {
            sb.Append($"; {kv.Key} x{kv.Value}");
        }

        return sb.ToString();
    }
}
