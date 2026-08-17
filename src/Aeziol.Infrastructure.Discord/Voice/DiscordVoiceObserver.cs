using System.Collections.Concurrent;
using System.Threading.Channels;
using Aeziol.Core.Abstractions;
using Aeziol.Core.Models;
using Aeziol.Infrastructure.Discord.OAuth;
using Aeziol.Infrastructure.Discord.Processes;
using Aeziol.Infrastructure.Discord.Rpc;

namespace Aeziol.Infrastructure.Discord.Voice;

public sealed record DiscordVoiceObserverOptions(
    string ClientId,
    Uri RedirectUri,
    TimeSpan ReconcileInterval,
    TimeSpan ConnectionRetryInterval,
    TimeSpan AuthorizationRetryInterval,
    string? DiscordExecutablePath)
{
    public static DiscordVoiceObserverOptions CreateDefault(
        string clientId,
        Uri redirectUri,
        string? discordExecutablePath = null) =>
        new(
            clientId,
            redirectUri,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(30),
            discordExecutablePath);
}

public sealed class DiscordVoiceObserver : IVoicePresenceObserver
{
    private const int PipeCount = 10;
    private readonly DiscordVoiceObserverOptions _options;
    private readonly DiscordOAuthSession _oauthSession;
    private readonly DiscordProcessMonitor _processMonitor;
    private readonly TimeProvider _timeProvider;
    private readonly DiscordRetrySchedule _retrySchedule;
    private readonly ConcurrentDictionary<int, DiscordSession> _sessions = new();
    private readonly SemaphoreSlim _reconcileGate = new(1, 1);
    private readonly Channel<bool> _reconcileRequests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true,
        SingleWriter = false,
    });
    private CancellationTokenSource? _lifetime;
    private Task? _runTask;
    private bool _disposed;
    private VoicePresenceState? _lastFallbackState;

    public DiscordVoiceObserver(
        DiscordVoiceObserverOptions options,
        IDiscordOAuthTokenExchange tokenExchange,
        IDiscordTokenStore tokenStore,
        TimeProvider? timeProvider = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ClientId);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ReconcileInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.ConnectionRetryInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.AuthorizationRetryInterval, TimeSpan.Zero);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _retrySchedule = new DiscordRetrySchedule(
            _timeProvider,
            options.ConnectionRetryInterval,
            options.AuthorizationRetryInterval);
        _oauthSession = new DiscordOAuthSession(
            options.ClientId,
            options.RedirectUri,
            tokenExchange,
            tokenStore,
            _timeProvider);
        _processMonitor = new DiscordProcessMonitor(options.DiscordExecutablePath);
    }

    public event EventHandler<VoiceObservationEventArgs>? ObservationReceived;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (_runTask is not null)
        {
            return Task.CompletedTask;
        }

        _lifetime = new CancellationTokenSource();
        _processMonitor.ProcessChanged += OnDiscordProcessChanged;
        _processMonitor.Start();
        _runTask = RunAsync(_lifetime.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_runTask is null)
        {
            return;
        }

        _lifetime?.Cancel();
        try
        {
            await _runTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_lifetime?.IsCancellationRequested == true)
        {
        }

        await CloseAllSessionsAsync().ConfigureAwait(false);
        _processMonitor.ProcessChanged -= OnDiscordProcessChanged;
        _processMonitor.Stop();
        _lifetime?.Dispose();
        _lifetime = null;
        _runTask = null;
    }

    public async Task<bool> AuthorizeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_runTask is null)
        {
            throw new InvalidOperationException("The Discord voice observer is not running.");
        }

        await _reconcileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_processMonitor.Current.Any(snapshot => snapshot.IsRunning))
            {
                EmitFallback(VoicePresenceState.DiscordAbsent);
                return false;
            }

            foreach (var index in Enumerable.Range(0, PipeCount).Where(index => !_sessions.ContainsKey(index)))
            {
                var result = await TryOpenSessionAsync(index, authorizeInteractively: true, cancellationToken)
                    .ConfigureAwait(false);
                if (result == SessionOpenResult.Opened)
                {
                    return true;
                }

                if (result == SessionOpenResult.AuthorizationRequired)
                {
                    return false;
                }
            }

            EmitFallback(VoicePresenceState.Unavailable);
            throw new DiscordRpcException(
                "Discord is running, but no compatible local RPC connection could be opened.");
        }
        finally
        {
            _reconcileGate.Release();
        }
    }

    public Task<bool> HasStoredAuthorizationAsync(CancellationToken cancellationToken = default) =>
        _oauthSession.HasStoredAuthorizationAsync(cancellationToken);

    public async Task<bool> RevokeAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _reconcileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CloseAllSessionsAsync().ConfigureAwait(false);
            var revoked = await _oauthSession.RevokeAsync(cancellationToken).ConfigureAwait(false);
            _retrySchedule.Reset();
            EmitFallback(VoicePresenceState.AuthorizationRequired);
            return revoked;
        }
        finally
        {
            _reconcileGate.Release();
        }
    }

    public async Task ForgetAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _reconcileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CloseAllSessionsAsync().ConfigureAwait(false);
            await _oauthSession.ClearAsync(cancellationToken).ConfigureAwait(false);
            _retrySchedule.Reset();
            EmitFallback(VoicePresenceState.AuthorizationRequired);
        }
        finally
        {
            _reconcileGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await StopAsync().ConfigureAwait(false);
        _processMonitor.Dispose();
        _oauthSession.Dispose();
        _reconcileGate.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _reconcileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await ReconcileAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _reconcileGate.Release();
            }

            using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var signal = _reconcileRequests.Reader.ReadAsync(waitCancellation.Token).AsTask();
            var safetyDelay = Task.Delay(_options.ReconcileInterval, waitCancellation.Token);
            _ = await Task.WhenAny(signal, safetyDelay).ConfigureAwait(false);
            await waitCancellation.CancelAsync().ConfigureAwait(false);
            try
            {
                await Task.WhenAll(signal, safetyDelay).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (waitCancellation.IsCancellationRequested)
            {
            }

            while (_reconcileRequests.Reader.TryRead(out _))
            {
            }
        }
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var hasDiscord = _processMonitor.Current.Any(snapshot => snapshot.IsRunning);
        if (!hasDiscord)
        {
            await CloseAllSessionsAsync().ConfigureAwait(false);
            EmitFallback(VoicePresenceState.DiscordAbsent);
            return;
        }

        if (_retrySchedule.CanAttemptAuthorization)
        {
            var candidates = Enumerable.Range(0, PipeCount)
                .Where(index => !_sessions.ContainsKey(index) && _retrySchedule.CanAttemptPipe(index))
                .ToArray();
            await Task.WhenAll(candidates.Select(index =>
                    TryOpenSessionAsync(index, authorizeInteractively: false, cancellationToken)))
                .ConfigureAwait(false);
        }

        foreach (var (index, session) in _sessions)
        {
            if (!session.Client.IsConnected && _sessions.TryRemove(index, out var removed))
            {
                Emit(new VoiceObservation(
                    removed.SourceId,
                    VoicePresenceState.Disconnected,
                    _timeProvider.GetUtcNow()));
                _retrySchedule.MarkPipeFailure(index);
                await removed.DisposeAsync().ConfigureAwait(false);
            }
        }

        if (_sessions.IsEmpty)
        {
            if (_lastFallbackState != VoicePresenceState.AuthorizationRequired)
            {
                EmitFallback(VoicePresenceState.Unavailable);
            }
        }
        else
        {
            _lastFallbackState = null;
        }
    }

    private async Task<SessionOpenResult> TryOpenSessionAsync(
        int pipeIndex,
        bool authorizeInteractively,
        CancellationToken cancellationToken)
    {
        var client = new DiscordRpcClient();
        var rpcConnected = false;
        var authenticationAttempted = false;
        var authenticated = false;
        try
        {
            await client.ConnectToPipeAsync(_options.ClientId, pipeIndex, cancellationToken).ConfigureAwait(false);
            rpcConnected = true;
            var sourceId = $"discord-ipc-{pipeIndex}";
            var interpreter = new DiscordVoiceEventInterpreter();
            var token = authorizeInteractively
                ? await _oauthSession.AuthorizeAsync(client, cancellationToken).ConfigureAwait(false)
                : await _oauthSession.TryGetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
            if (token is null)
            {
                _retrySchedule.MarkAuthorizationFailure();
                EmitFallback(VoicePresenceState.AuthorizationRequired);
                await client.DisposeAsync().ConfigureAwait(false);
                return SessionOpenResult.AuthorizationRequired;
            }

            authenticationAttempted = true;
            await client.AuthenticateAsync(token, cancellationToken).ConfigureAwait(false);
            authenticated = true;
            var selected = await client.GetSelectedVoiceChannelAsync(cancellationToken).ConfigureAwait(false);
            var session = new DiscordSession(sourceId, client, interpreter, OnRpcEvent, OnRpcConnectionClosed);
            await client.SubscribeToVoiceEventsAsync(cancellationToken).ConfigureAwait(false);
            if (!_sessions.TryAdd(pipeIndex, session))
            {
                await session.DisposeAsync().ConfigureAwait(false);
                return SessionOpenResult.Opened;
            }

            _retrySchedule.MarkSuccess(pipeIndex);
            Emit(interpreter.FromInitialSelection(sourceId, selected, _timeProvider.GetUtcNow()));
            return SessionOpenResult.Opened;
        }
        catch (DiscordRpcException)
        {
            if (authenticationAttempted && !authenticated)
            {
                await _oauthSession.ClearAsync(cancellationToken).ConfigureAwait(false);
                _retrySchedule.MarkAuthorizationFailure();
                EmitFallback(VoicePresenceState.AuthorizationRequired);
                await client.DisposeAsync().ConfigureAwait(false);
                if (authorizeInteractively)
                {
                    throw;
                }

                return SessionOpenResult.AuthorizationRequired;
            }

            if (authorizeInteractively && rpcConnected)
            {
                await client.DisposeAsync().ConfigureAwait(false);
                throw;
            }

            _retrySchedule.MarkPipeFailure(pipeIndex);
            await client.DisposeAsync().ConfigureAwait(false);
            return SessionOpenResult.PipeUnavailable;
        }
        catch (IOException)
        {
            _retrySchedule.MarkPipeFailure(pipeIndex);
            await client.DisposeAsync().ConfigureAwait(false);
            if (authorizeInteractively && rpcConnected)
            {
                throw;
            }

            return SessionOpenResult.PipeUnavailable;
        }
        catch (TimeoutException)
        {
            _retrySchedule.MarkPipeFailure(pipeIndex);
            await client.DisposeAsync().ConfigureAwait(false);
            if (authorizeInteractively && rpcConnected)
            {
                throw;
            }

            return SessionOpenResult.PipeUnavailable;
        }
        catch (HttpRequestException)
        {
            _retrySchedule.MarkAuthorizationFailure();
            EmitFallback(VoicePresenceState.AuthorizationRequired);
            await client.DisposeAsync().ConfigureAwait(false);
            if (authorizeInteractively)
            {
                throw;
            }

            return SessionOpenResult.AuthorizationRequired;
        }
    }

    private enum SessionOpenResult
    {
        Opened,
        PipeUnavailable,
        AuthorizationRequired,
    }

    private void OnRpcEvent(DiscordSession session, DiscordRpcEventArgs rpcEvent)
    {
        var observation = session.Interpreter.Interpret(
            session.SourceId,
            rpcEvent,
            _timeProvider.GetUtcNow());
        if (observation is not null)
        {
            Emit(observation);
        }
    }

    private void OnRpcConnectionClosed(DiscordSession session) => RequestReconcile();

    private void OnDiscordProcessChanged(object? sender, DiscordProcessChangedEventArgs eventArgs)
    {
        _retrySchedule.Reset();
        RequestReconcile();
    }

    private void RequestReconcile() => _reconcileRequests.Writer.TryWrite(true);

    private void EmitFallback(VoicePresenceState state)
    {
        if (_lastFallbackState == state)
        {
            return;
        }

        _lastFallbackState = state;
        Emit(new VoiceObservation("discord-local", state, _timeProvider.GetUtcNow()));
    }

    private void Emit(VoiceObservation observation) =>
        ObservationReceived?.Invoke(this, new VoiceObservationEventArgs(observation));

    private async Task CloseAllSessionsAsync()
    {
        foreach (var (index, _) in _sessions)
        {
            if (_sessions.TryRemove(index, out var session))
            {
                Emit(new VoiceObservation(
                    session.SourceId,
                    VoicePresenceState.DiscordAbsent,
                    _timeProvider.GetUtcNow()));
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private sealed class DiscordSession : IAsyncDisposable
    {
        private readonly EventHandler<DiscordRpcEventArgs> _handler;
        private readonly EventHandler<DiscordRpcConnectionClosedEventArgs> _closedHandler;

        public DiscordSession(
            string sourceId,
            DiscordRpcClient client,
            DiscordVoiceEventInterpreter interpreter,
            Action<DiscordSession, DiscordRpcEventArgs> onEvent,
            Action<DiscordSession> onConnectionClosed)
        {
            SourceId = sourceId;
            Client = client;
            Interpreter = interpreter;
            _handler = (_, eventArgs) => onEvent(this, eventArgs);
            _closedHandler = (_, _) => onConnectionClosed(this);
            Client.EventReceived += _handler;
            Client.ConnectionClosed += _closedHandler;
        }

        public string SourceId { get; }

        public DiscordRpcClient Client { get; }

        public DiscordVoiceEventInterpreter Interpreter { get; }

        public async ValueTask DisposeAsync()
        {
            Client.EventReceived -= _handler;
            Client.ConnectionClosed -= _closedHandler;
            await Client.DisposeAsync().ConfigureAwait(false);
        }
    }
}
