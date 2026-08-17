using System.Collections.Concurrent;

namespace Aeziol.Infrastructure.Discord.Voice;

internal sealed class DiscordRetrySchedule(
    TimeProvider timeProvider,
    TimeSpan connectionRetryInterval,
    TimeSpan authorizationRetryInterval)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly TimeSpan _connectionRetryInterval = connectionRetryInterval;
    private readonly TimeSpan _authorizationRetryInterval = authorizationRetryInterval;
    private readonly ConcurrentDictionary<int, DateTimeOffset> _pipeRetryAfter = new();
    private readonly Lock _authorizationLock = new();
    private DateTimeOffset? _authorizationRetryAfter;

    public bool CanAttemptAuthorization
    {
        get
        {
            lock (_authorizationLock)
            {
                return _authorizationRetryAfter is not { } retryAfter
                    || _timeProvider.GetUtcNow() >= retryAfter;
            }
        }
    }

    public bool CanAttemptPipe(int pipeIndex) =>
        !_pipeRetryAfter.TryGetValue(pipeIndex, out var retryAfter)
        || _timeProvider.GetUtcNow() >= retryAfter;

    public void MarkPipeFailure(int pipeIndex) =>
        _pipeRetryAfter[pipeIndex] = _timeProvider.GetUtcNow() + _connectionRetryInterval;

    public void MarkAuthorizationFailure()
    {
        lock (_authorizationLock)
        {
            _authorizationRetryAfter = _timeProvider.GetUtcNow() + _authorizationRetryInterval;
        }
    }

    public void MarkSuccess(int pipeIndex)
    {
        _pipeRetryAfter.TryRemove(pipeIndex, out _);
        lock (_authorizationLock)
        {
            _authorizationRetryAfter = null;
        }
    }

    public void Reset()
    {
        _pipeRetryAfter.Clear();
        lock (_authorizationLock)
        {
            _authorizationRetryAfter = null;
        }
    }
}
