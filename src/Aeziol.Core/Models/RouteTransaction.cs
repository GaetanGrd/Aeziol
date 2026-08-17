namespace Aeziol.Core.Models;

public enum RouteTransactionState
{
    Prepared,
    Applying,
    Applied,
    Restoring,
    AwaitingRecoveryConfirmation,
    AbandonedByUser,
    Failed,
}
public sealed record RouteTransaction(
    Guid Id,
    AudioRouteSnapshot Source,
    string TargetEndpointId,
    IReadOnlySet<AudioRole> Roles,
    RouteTransactionState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? FailureCode = null)
{
    public RouteTransaction WithState(RouteTransactionState state, DateTimeOffset now, string? failureCode = null) =>
        this with { State = state, UpdatedAt = now, FailureCode = failureCode };
}
