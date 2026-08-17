using Aeziol.Core.Abstractions;
using Aeziol.Core.Models;

namespace Aeziol.Core.Routing;

public sealed class AudioRoutingOrchestrator : IDisposable
{
    private readonly IAudioRouteController _controller;
    private readonly IRouteTransactionStore _store;
    private readonly IExclusionPolicy _exclusionPolicy;
    private readonly IClock _clock;
    private readonly IReadOnlySet<AudioRole> _roles;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RouteTransaction? _activeTransaction;
    private bool _internalMutation;
    private bool _disposed;

    public AudioRoutingOrchestrator(
        IAudioRouteController controller,
        IRouteTransactionStore store,
        IExclusionPolicy exclusionPolicy,
        IClock clock,
        IReadOnlySet<AudioRole> roles)
    {
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _exclusionPolicy = exclusionPolicy ?? throw new ArgumentNullException(nameof(exclusionPolicy));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _roles = roles is { Count: > 0 }
            ? new HashSet<AudioRole>(roles)
            : throw new ArgumentException("At least one audio role is required.", nameof(roles));
    }

    public RouteTransaction? ActiveTransaction => _activeTransaction;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _activeTransaction = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RoutingResult> ActivateAsync(
        AutomationRule rule,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(rule);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_activeTransaction is not null)
            {
                return new RoutingResult(RoutingOutcome.NoChangeNeeded, _activeTransaction);
            }

            if (!await _controller.IsEndpointUsableAsync(rule.TargetEndpointId, cancellationToken).ConfigureAwait(false))
            {
                return new RoutingResult(RoutingOutcome.TargetUnavailable);
            }

            var source = await _controller.CaptureAsync(_roles, cancellationToken).ConfigureAwait(false);
            if (_roles.Any(role => string.IsNullOrWhiteSpace(source.Get(role))))
            {
                return new RoutingResult(RoutingOutcome.SourceUnavailable);
            }

            if (source.Endpoints.Values.Any(_exclusionPolicy.IsExcluded))
            {
                return new RoutingResult(RoutingOutcome.SkippedByExclusion);
            }

            if (_roles.All(role => string.Equals(source.Get(role), rule.TargetEndpointId, StringComparison.OrdinalIgnoreCase)))
            {
                return new RoutingResult(RoutingOutcome.NoChangeNeeded);
            }

            var now = _clock.UtcNow;
            var transaction = new RouteTransaction(
                Guid.NewGuid(),
                source,
                rule.TargetEndpointId,
                _roles,
                RouteTransactionState.Prepared,
                now,
                now);
            await _store.SaveAsync(transaction, cancellationToken).ConfigureAwait(false);

            transaction = transaction.WithState(RouteTransactionState.Applying, _clock.UtcNow);
            await _store.SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
            _activeTransaction = transaction;

            try
            {
                _internalMutation = true;
                await _controller.ApplyAsync(rule.TargetEndpointId, _roles, cancellationToken).ConfigureAwait(false);
                if (!await _controller.VerifyAsync(rule.TargetEndpointId, _roles, cancellationToken).ConfigureAwait(false))
                {
                    throw new AudioRouteVerificationException("The target route could not be verified.");
                }

                transaction = transaction.WithState(RouteTransactionState.Applied, _clock.UtcNow);
                await _store.SaveAsync(transaction, cancellationToken).ConfigureAwait(false);
                _activeTransaction = transaction;
                return new RoutingResult(RoutingOutcome.Applied, transaction);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return await RollBackAfterFailedApplyAsync(transaction, exception).ConfigureAwait(false);
            }
            finally
            {
                _internalMutation = false;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RoutingResult> RestoreAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await RestoreUnderLockAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RoutingResult> RetargetAsync(
        string targetEndpointId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(targetEndpointId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_activeTransaction is not { State: RouteTransactionState.Applied } transaction)
            {
                return new RoutingResult(RoutingOutcome.NoChangeNeeded, _activeTransaction);
            }

            if (string.Equals(
                    transaction.TargetEndpointId,
                    targetEndpointId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new RoutingResult(RoutingOutcome.NoChangeNeeded, transaction);
            }

            if (!await _controller.IsEndpointUsableAsync(targetEndpointId, cancellationToken).ConfigureAwait(false))
            {
                return new RoutingResult(RoutingOutcome.TargetUnavailable, transaction);
            }

            var routeBeforeRetarget = await _controller.CaptureAsync(transaction.Roles, cancellationToken)
                .ConfigureAwait(false);
            var retargeting = transaction with
            {
                TargetEndpointId = targetEndpointId,
                State = RouteTransactionState.Applying,
                UpdatedAt = _clock.UtcNow,
                FailureCode = null,
            };
            await _store.SaveAsync(retargeting, cancellationToken).ConfigureAwait(false);
            _activeTransaction = retargeting;

            try
            {
                _internalMutation = true;
                await _controller.ApplyAsync(targetEndpointId, transaction.Roles, cancellationToken)
                    .ConfigureAwait(false);
                if (!await _controller.VerifyAsync(targetEndpointId, transaction.Roles, cancellationToken)
                        .ConfigureAwait(false))
                {
                    throw new AudioRouteVerificationException("The new target route could not be verified.");
                }

                var retargeted = retargeting.WithState(RouteTransactionState.Applied, _clock.UtcNow);
                await _store.SaveAsync(retargeted, cancellationToken).ConfigureAwait(false);
                _activeTransaction = retargeted;
                return new RoutingResult(RoutingOutcome.Retargeted, retargeted);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return await RollBackAfterFailedRetargetAsync(
                    transaction,
                    retargeting,
                    routeBeforeRetarget,
                    exception).ConfigureAwait(false);
            }
            finally
            {
                _internalMutation = false;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> HandleDefaultEndpointChangedAsync(
        AudioRole role,
        string endpointId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_internalMutation || _activeTransaction is not { State: RouteTransactionState.Applied } transaction)
            {
                return false;
            }

            if (!transaction.Roles.Contains(role)
                || string.Equals(endpointId, transaction.TargetEndpointId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var abandoned = transaction.WithState(RouteTransactionState.AbandonedByUser, _clock.UtcNow);
            await _store.SaveAsync(abandoned, cancellationToken).ConfigureAwait(false);
            await _store.DeleteAsync(cancellationToken).ConfigureAwait(false);
            _activeTransaction = null;
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RecoveryProposal?> InspectRecoveryAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _activeTransaction ??= await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (_activeTransaction is null)
            {
                return null;
            }

            var current = await _controller.CaptureAsync(_activeTransaction.Roles, cancellationToken).ConfigureAwait(false);
            var matchesTarget = _activeTransaction.Roles.All(role =>
                string.Equals(current.Get(role), _activeTransaction.TargetEndpointId, StringComparison.OrdinalIgnoreCase));
            var sourcesAvailable = await AreSourceEndpointsAvailableAsync(_activeTransaction, cancellationToken)
                .ConfigureAwait(false);

            return new RecoveryProposal(
                _activeTransaction,
                current,
                matchesTarget,
                matchesTarget && sourcesAvailable);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<RoutingResult> ResolveRecoveryAsync(
        bool restore,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _activeTransaction ??= await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (_activeTransaction is null)
            {
                return new RoutingResult(RoutingOutcome.NoChangeNeeded);
            }

            if (!restore)
            {
                var abandoned = _activeTransaction.WithState(RouteTransactionState.AbandonedByUser, _clock.UtcNow);
                await _store.SaveAsync(abandoned, cancellationToken).ConfigureAwait(false);
                await _store.DeleteAsync(cancellationToken).ConfigureAwait(false);
                _activeTransaction = null;
                return new RoutingResult(RoutingOutcome.AbandonedByUser, abandoned);
            }

            var current = await _controller.CaptureAsync(_activeTransaction.Roles, cancellationToken).ConfigureAwait(false);
            var stillMatchesTarget = _activeTransaction.Roles.All(role =>
                string.Equals(current.Get(role), _activeTransaction.TargetEndpointId, StringComparison.OrdinalIgnoreCase));
            if (!stillMatchesTarget)
            {
                return new RoutingResult(
                    RoutingOutcome.AbandonedByUser,
                    _activeTransaction,
                    "manual-route-change-detected");
            }

            return await RestoreUnderLockAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _gate.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task<RoutingResult> RestoreUnderLockAsync(CancellationToken cancellationToken)
    {
        if (_activeTransaction is null)
        {
            return new RoutingResult(RoutingOutcome.NoChangeNeeded);
        }

        if (!await AreSourceEndpointsAvailableAsync(_activeTransaction, cancellationToken).ConfigureAwait(false))
        {
            return new RoutingResult(RoutingOutcome.SourceUnavailable, _activeTransaction);
        }

        var restoring = _activeTransaction.WithState(RouteTransactionState.Restoring, _clock.UtcNow);
        await _store.SaveAsync(restoring, cancellationToken).ConfigureAwait(false);
        _activeTransaction = restoring;

        try
        {
            _internalMutation = true;
            await _controller.RestoreAsync(restoring.Source, cancellationToken).ConfigureAwait(false);
            if (!await _controller.VerifyAsync(restoring.Source, cancellationToken).ConfigureAwait(false))
            {
                throw new AudioRouteVerificationException("The original route could not be verified.");
            }

            await _store.DeleteAsync(cancellationToken).ConfigureAwait(false);
            _activeTransaction = null;
            return new RoutingResult(RoutingOutcome.Restored, restoring);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failed = restoring.WithState(RouteTransactionState.Failed, _clock.UtcNow, GetFailureCode(exception));
            await _store.SaveAsync(failed, CancellationToken.None).ConfigureAwait(false);
            _activeTransaction = failed;
            return new RoutingResult(RoutingOutcome.Failed, failed, failed.FailureCode);
        }
        finally
        {
            _internalMutation = false;
        }
    }

    private async Task<RoutingResult> RollBackAfterFailedApplyAsync(
        RouteTransaction transaction,
        Exception applyException)
    {
        try
        {
            await _controller.RestoreAsync(transaction.Source, CancellationToken.None).ConfigureAwait(false);
            if (!await _controller.VerifyAsync(transaction.Source, CancellationToken.None).ConfigureAwait(false))
            {
                throw new AudioRouteVerificationException("Rollback verification failed.");
            }

            await _store.DeleteAsync(CancellationToken.None).ConfigureAwait(false);
            _activeTransaction = null;
            return new RoutingResult(RoutingOutcome.RolledBack, transaction, GetFailureCode(applyException));
        }
        catch (Exception rollbackException)
        {
            var failed = transaction.WithState(
                RouteTransactionState.Failed,
                _clock.UtcNow,
                $"{GetFailureCode(applyException)}+rollback:{GetFailureCode(rollbackException)}");
            await _store.SaveAsync(failed, CancellationToken.None).ConfigureAwait(false);
            _activeTransaction = failed;
            return new RoutingResult(RoutingOutcome.Failed, failed, failed.FailureCode);
        }
    }

    private async Task<RoutingResult> RollBackAfterFailedRetargetAsync(
        RouteTransaction previousTransaction,
        RouteTransaction retargetingTransaction,
        AudioRouteSnapshot routeBeforeRetarget,
        Exception applyException)
    {
        try
        {
            await _controller.RestoreAsync(routeBeforeRetarget, CancellationToken.None).ConfigureAwait(false);
            if (!await _controller.VerifyAsync(routeBeforeRetarget, CancellationToken.None).ConfigureAwait(false))
            {
                throw new AudioRouteVerificationException("Retarget rollback verification failed.");
            }

            var restoredTransaction = previousTransaction.WithState(RouteTransactionState.Applied, _clock.UtcNow);
            await _store.SaveAsync(restoredTransaction, CancellationToken.None).ConfigureAwait(false);
            _activeTransaction = restoredTransaction;
            return new RoutingResult(RoutingOutcome.RolledBack, restoredTransaction, GetFailureCode(applyException));
        }
        catch (Exception rollbackException)
        {
            var failed = retargetingTransaction.WithState(
                RouteTransactionState.Failed,
                _clock.UtcNow,
                $"{GetFailureCode(applyException)}+rollback:{GetFailureCode(rollbackException)}");
            await _store.SaveAsync(failed, CancellationToken.None).ConfigureAwait(false);
            _activeTransaction = failed;
            return new RoutingResult(RoutingOutcome.Failed, failed, failed.FailureCode);
        }
    }

    private async Task<bool> AreSourceEndpointsAvailableAsync(
        RouteTransaction transaction,
        CancellationToken cancellationToken)
    {
        foreach (var endpointId in transaction.Source.Endpoints.Values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!await _controller.IsEndpointUsableAsync(endpointId, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
        }

        return true;
    }

    private static string GetFailureCode(Exception exception) => exception switch
    {
        AudioRouteVerificationException => "verification-failed",
        UnauthorizedAccessException => "access-denied",
        _ => "audio-route-operation-failed",
    };

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

public sealed class AudioRouteVerificationException : Exception
{
    public AudioRouteVerificationException(string message)
        : base(message)
    {
    }
}
