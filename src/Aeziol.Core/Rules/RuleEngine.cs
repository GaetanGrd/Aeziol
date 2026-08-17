using Aeziol.Core.Models;

namespace Aeziol.Core.Rules;

public static class RuleEngine
{
    public static RuleDecision Evaluate(string triggerId, IEnumerable<AutomationRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(triggerId);
        ArgumentNullException.ThrowIfNull(rules);

        var matches = rules
            .Where(rule => rule.IsEnabled && string.Equals(rule.TriggerId, triggerId, StringComparison.Ordinal))
            .OrderByDescending(rule => rule.Priority)
            .ThenBy(rule => rule.Id)
            .ToArray();

        if (matches.Length == 0)
        {
            return new RuleDecision(null, matches, false);
        }

        var highestPriority = matches[0].Priority;
        var conflict = matches.Count(rule => rule.Priority == highestPriority) > 1;
        return new RuleDecision(conflict ? null : matches[0], matches, conflict);
    }
}
