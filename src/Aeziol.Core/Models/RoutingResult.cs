namespace Aeziol.Core.Models;

public enum RoutingOutcome
{
    Applied,
    Retargeted,
    Restored,
    NoChangeNeeded,
    SkippedByExclusion,
    TargetUnavailable,
    SourceUnavailable,
    RolledBack,
    AbandonedByUser,
    RecoveryConfirmationRequired,
    Failed,
}

public sealed record RoutingResult(
    RoutingOutcome Outcome,
    RouteTransaction? Transaction = null,
    string? ErrorCode = null);

public sealed record RecoveryProposal(
    RouteTransaction Transaction,
    AudioRouteSnapshot Current,
    bool CurrentRouteStillMatchesTarget,
    bool CanSafelyRestore);
