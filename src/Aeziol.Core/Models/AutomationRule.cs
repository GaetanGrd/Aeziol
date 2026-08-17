namespace Aeziol.Core.Models;

public sealed record AutomationRule(
    Guid Id,
    string TriggerId,
    string TargetEndpointId,
    int Priority,
    bool IsEnabled = true);

public sealed record RuleDecision(
    AutomationRule? SelectedRule,
    IReadOnlyList<AutomationRule> MatchingRules,
    bool HasPriorityConflict);
