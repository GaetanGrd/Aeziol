using System.Net.Http;
using Aeziol.App.Settings;
using Aeziol.Core.Abstractions;
using Aeziol.Core.Models;
using Aeziol.Core.Persistence;
using Aeziol.Core.Policies;
using Aeziol.Core.Routing;
using Aeziol.Core.Voice;
using Aeziol.Infrastructure.Discord.OAuth;
using Aeziol.Infrastructure.Discord.Rpc;
using Aeziol.Infrastructure.Discord.Voice;
using Aeziol.Infrastructure.Windows.Audio;

namespace Aeziol.App.Services;

public sealed class VoiceStateChangedEventArgs(VoicePresenceState state) : EventArgs
{
    public VoicePresenceState State { get; } = state;
}

public sealed class RoutingStateChangedEventArgs(RoutingResult result) : EventArgs
{
    public RoutingResult Result { get; } = result;
}

public sealed class DiscordAuthorizationChangedEventArgs(bool isAuthorized) : EventArgs
{
    public bool IsAuthorized { get; } = isAuthorized;
}

public sealed class AudioEndpointsChangedEventArgs : EventArgs
{
}

public sealed class AeziolRuntime : IAsyncDisposable
{
    private static readonly IReadOnlySet<AudioRole> AllRoles = new HashSet<AudioRole>
    {
        AudioRole.Console,
        AudioRole.Multimedia,
        AudioRole.Communications,
    };

    private readonly AppPaths _paths;
    private readonly AppLogger _logger;
    private readonly WindowsAudioRouteController _audioController = new();
    private readonly EndpointExclusionPolicy _exclusionPolicy;
    private readonly JsonRouteTransactionStore _transactionStore;
    private readonly AudioRoutingOrchestrator _orchestrator;
    private readonly WindowsAudioEndpointObserver _audioObserver;
    private readonly HttpClient _httpClient = new();
    private readonly WindowsCredentialDiscordTokenStore _discordTokenStore = new();
    private VoicePresenceCoordinator _voiceCoordinator;
    private DiscordVoiceObserver? _discordObserver;
    private CancellationTokenSource? _exitTimerCancellation;
    private bool _shuttingDown;
    private bool _disposed;
    private bool _isDiscordAuthorized;

    public AeziolRuntime(AppSettings settings, AppPaths paths, AppLogger logger)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _exclusionPolicy = new EndpointExclusionPolicy(settings.ExcludedEndpointIds);
        _transactionStore = new JsonRouteTransactionStore(paths.TransactionFile);
        _orchestrator = new AudioRoutingOrchestrator(
            _audioController,
            _transactionStore,
            _exclusionPolicy,
            new SystemClock(),
            AllRoles);
        _voiceCoordinator = CreateCoordinator(settings);
        _audioObserver = new WindowsAudioEndpointObserver();
        _audioObserver.DefaultEndpointChanged += OnDefaultEndpointChanged;
        _audioObserver.EndpointCatalogChanged += OnEndpointCatalogChanged;
    }

    public event EventHandler<VoiceStateChangedEventArgs>? VoiceStateChanged;

    public event EventHandler<RoutingStateChangedEventArgs>? RoutingStateChanged;

    public event EventHandler<DiscordAuthorizationChangedEventArgs>? DiscordAuthorizationChanged;

    public event EventHandler<AudioEndpointsChangedEventArgs>? AudioEndpointsChanged;

    public AppSettings Settings { get; private set; }

    public VoicePresenceState VoiceState => _voiceCoordinator.AggregateState;

    public bool IsDiscordAuthorized => _isDiscordAuthorized;

    public RouteTransaction? ActiveRouteTransaction => _orchestrator.ActiveTransaction;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _orchestrator.InitializeAsync(cancellationToken).ConfigureAwait(false);
        await RestartDiscordObserverAsync(cancellationToken).ConfigureAwait(false);
        await _logger.WriteAsync("information", "runtime-initialized", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<IReadOnlyList<AudioEndpoint>> GetEndpointsAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => _audioController.GetRenderEndpointsAsync(cancellationToken), cancellationToken);

    public Task<AudioRouteSnapshot> GetCurrentRouteAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() => _audioController.CaptureAsync(AllRoles, cancellationToken), cancellationToken);

    public Task SetCurrentOutputAsync(string endpointId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        return Task.Run(() => SetCurrentOutputCoreAsync(endpointId, cancellationToken), cancellationToken);
    }

    public async Task<bool> AuthorizeDiscordAsync(CancellationToken cancellationToken = default)
    {
        if (_discordObserver is null)
        {
            return false;
        }

        try
        {
            var authorized = await _discordObserver.AuthorizeAsync(cancellationToken).ConfigureAwait(false);
            if (authorized)
            {
                SetDiscordAuthorization(true);
            }
            await _logger.WriteAsync(
                authorized ? "information" : "warning",
                authorized ? "discord-authorization-succeeded" : "discord-authorization-failed",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return authorized;
        }
        catch (Exception exception)
        {
            await _logger.WriteAsync(
                "error",
                "discord-authorization-error",
                new { exception.Message },
                cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<bool> RevokeDiscordAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        bool revoked;
        if (_discordObserver is not null)
        {
            revoked = await _discordObserver.RevokeAuthorizationAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var stored = await _discordTokenStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (stored is null)
            {
                SetDiscordAuthorization(false);
                return false;
            }

            var exchange = new DiscordPublicClientTokenExchange(_httpClient);
            await exchange.RevokeAsync(Settings.DiscordClientId, stored.AccessToken, cancellationToken)
                .ConfigureAwait(false);
            await _discordTokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            revoked = true;
        }

        _exitTimerCancellation?.Cancel();
        if (_orchestrator.ActiveTransaction is not null)
        {
            await RestoreAndPublishAsync(cancellationToken).ConfigureAwait(false);
        }

        SetDiscordAuthorization(false);
        await _logger.WriteAsync("information", "discord-authorization-revoked", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return revoked;
    }

    public async Task ForgetDiscordAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        if (_discordObserver is not null)
        {
            await _discordObserver.ForgetAuthorizationAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _discordTokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
        }

        _exitTimerCancellation?.Cancel();
        if (_orchestrator.ActiveTransaction is not null)
        {
            await RestoreAndPublishAsync(cancellationToken).ConfigureAwait(false);
        }

        SetDiscordAuthorization(false);
        await _logger.WriteAsync("information", "discord-authorization-forgotten-locally", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<RecoveryProposal?> InspectRecoveryAsync(CancellationToken cancellationToken = default) =>
        _orchestrator.InspectRecoveryAsync(cancellationToken);

    public async Task ResolveRecoveryAsync(bool restore, CancellationToken cancellationToken = default)
    {
        var result = await _orchestrator.ResolveRecoveryAsync(restore, cancellationToken).ConfigureAwait(false);
        RoutingStateChanged?.Invoke(this, new RoutingStateChangedEventArgs(result));
        await LogRoutingAsync(result, cancellationToken).ConfigureAwait(false);
    }

    public Task ForceRestoreAsync(CancellationToken cancellationToken = default) =>
        RestoreAndPublishAsync(cancellationToken);

    public async Task UpdateSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var previous = Settings;
        var targetChanged = !string.Equals(
            previous.TargetEndpointId,
            settings.TargetEndpointId,
            StringComparison.OrdinalIgnoreCase);
        var gracePeriodChanged = previous.ExitGracePeriodSeconds != settings.ExitGracePeriodSeconds;
        var observerMustRestart = previous.AutomationEnabled != settings.AutomationEnabled
            || !string.Equals(previous.DiscordClientId, settings.DiscordClientId, StringComparison.Ordinal)
            || !string.Equals(previous.DiscordRedirectUri, settings.DiscordRedirectUri, StringComparison.Ordinal)
            || !string.Equals(previous.DiscordExecutablePath, settings.DiscordExecutablePath, StringComparison.OrdinalIgnoreCase)
            || gracePeriodChanged;

        if (_orchestrator.ActiveTransaction is not null && !settings.AutomationEnabled)
        {
            await RestoreAndPublishAsync(cancellationToken).ConfigureAwait(false);
        }

        Settings = settings;
        _exclusionPolicy.Replace(settings.ExcludedEndpointIds);
        if (gracePeriodChanged)
        {
            _voiceCoordinator = CreateCoordinator(settings);
        }

        var retargetedActiveRoute = false;
        if (targetChanged
            && settings.AutomationEnabled
            && !string.IsNullOrWhiteSpace(settings.TargetEndpointId)
            && _orchestrator.ActiveTransaction is not null)
        {
            var result = await _orchestrator.RetargetAsync(settings.TargetEndpointId, cancellationToken)
                .ConfigureAwait(false);
            retargetedActiveRoute = true;
            RoutingStateChanged?.Invoke(this, new RoutingStateChangedEventArgs(result));
            await LogRoutingAsync(result, cancellationToken).ConfigureAwait(false);
        }

        if (observerMustRestart)
        {
            await RestartDiscordObserverAsync(cancellationToken).ConfigureAwait(false);
        }

        if (targetChanged
            && !retargetedActiveRoute
            && !observerMustRestart
            && settings.AutomationEnabled
            && _voiceCoordinator.IsSessionActive)
        {
            await ActivateConfiguredRouteAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        if (_shuttingDown || _disposed)
        {
            return;
        }

        _shuttingDown = true;
        _exitTimerCancellation?.Cancel();
        if (_discordObserver is not null)
        {
            _discordObserver.ObservationReceived -= OnVoiceObservation;
            await _discordObserver.DisposeAsync().ConfigureAwait(false);
            _discordObserver = null;
        }

        if (_orchestrator.ActiveTransaction is not null)
        {
            await RestoreAndPublishAsync(cancellationToken).ConfigureAwait(false);
        }

        await _logger.WriteAsync("information", "runtime-shutdown", cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await ShutdownAsync().ConfigureAwait(false);
        _exitTimerCancellation?.Dispose();
        _audioObserver.DefaultEndpointChanged -= OnDefaultEndpointChanged;
        _audioObserver.EndpointCatalogChanged -= OnEndpointCatalogChanged;
        _audioObserver.Dispose();
        _orchestrator.Dispose();
        _transactionStore.Dispose();
        _httpClient.Dispose();
        _logger.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task RestartDiscordObserverAsync(CancellationToken cancellationToken)
    {
        if (_discordObserver is not null)
        {
            _discordObserver.ObservationReceived -= OnVoiceObservation;
            await _discordObserver.DisposeAsync().ConfigureAwait(false);
            _discordObserver = null;
        }

        if (!Settings.AutomationEnabled || string.IsNullOrWhiteSpace(Settings.DiscordClientId))
        {
            SetDiscordAuthorization(
                await _discordTokenStore.LoadAsync(cancellationToken).ConfigureAwait(false) is not null);
            ApplyObservation(new VoiceObservation(
                "discord-local",
                string.IsNullOrWhiteSpace(Settings.DiscordClientId)
                    ? VoicePresenceState.AuthorizationRequired
                    : VoicePresenceState.Unavailable,
                DateTimeOffset.UtcNow));
            return;
        }

        if (!Uri.TryCreate(Settings.DiscordRedirectUri, UriKind.Absolute, out var redirectUri))
        {
            ApplyObservation(new VoiceObservation(
                "discord-local",
                VoicePresenceState.AuthorizationRequired,
                DateTimeOffset.UtcNow));
            return;
        }

        var exchange = new DiscordPublicClientTokenExchange(_httpClient);
        _discordObserver = new DiscordVoiceObserver(
            DiscordVoiceObserverOptions.CreateDefault(
                Settings.DiscordClientId,
                redirectUri,
                Settings.DiscordExecutablePath),
            exchange,
            _discordTokenStore);
        _discordObserver.ObservationReceived += OnVoiceObservation;
        await _discordObserver.StartAsync(cancellationToken).ConfigureAwait(false);
        SetDiscordAuthorization(
            await _discordObserver.HasStoredAuthorizationAsync(cancellationToken).ConfigureAwait(false));
    }

    private async void OnVoiceObservation(object? sender, VoiceObservationEventArgs eventArgs)
    {
        try
        {
            await HandleObservationAsync(eventArgs.Observation).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await _logger.WriteAsync("error", "voice-observation-failed", new { exception.Message })
                .ConfigureAwait(false);
        }
    }

    private async Task HandleObservationAsync(VoiceObservation observation)
    {
        var transition = ApplyObservation(observation);
        if (transition.EnteredVoice)
        {
            _exitTimerCancellation?.Cancel();
            await ActivateConfiguredRouteAsync().ConfigureAwait(false);
        }

        if (transition.ExitCanceled)
        {
            _exitTimerCancellation?.Cancel();
        }

        if (transition.ExitScheduled && transition.ExitDueAt is { } dueAt)
        {
            ScheduleExit(dueAt);
        }
    }

    private VoiceTransition ApplyObservation(VoiceObservation observation)
    {
        if (observation.State == VoicePresenceState.AuthorizationRequired)
        {
            SetDiscordAuthorization(false);
        }
        else if (observation.State is VoicePresenceState.OutOfVoice
                 or VoicePresenceState.Connecting
                 or VoicePresenceState.Connected
                 or VoicePresenceState.ChangingChannel
                 or VoicePresenceState.Reconnecting
                 or VoicePresenceState.Disconnected)
        {
            SetDiscordAuthorization(true);
        }

        var transition = _voiceCoordinator.Apply(observation);
        VoiceStateChanged?.Invoke(this, new VoiceStateChangedEventArgs(transition.AggregateState));
        return transition;
    }

    private void ScheduleExit(DateTimeOffset dueAt)
    {
        _exitTimerCancellation?.Cancel();
        _exitTimerCancellation?.Dispose();
        _exitTimerCancellation = new CancellationTokenSource();
        _ = CompleteExitAsync(dueAt, _exitTimerCancellation.Token);
    }

    private async Task CompleteExitAsync(DateTimeOffset dueAt, CancellationToken cancellationToken)
    {
        try
        {
            var delay = dueAt - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            var transition = _voiceCoordinator.Tick(DateTimeOffset.UtcNow);
            VoiceStateChanged?.Invoke(this, new VoiceStateChangedEventArgs(transition.AggregateState));
            if (transition.ExitedVoice && !_voiceCoordinator.IsSessionActive)
            {
                await RestoreAndPublishAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async void OnDefaultEndpointChanged(object? sender, DefaultAudioEndpointChangedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.EndpointId))
        {
            return;
        }

        try
        {
            var abandoned = await _orchestrator.HandleDefaultEndpointChangedAsync(
                eventArgs.Role,
                eventArgs.EndpointId).ConfigureAwait(false);
            if (abandoned)
            {
                await _logger.WriteAsync("information", "manual-audio-change-won").ConfigureAwait(false);
            }

            AudioEndpointsChanged?.Invoke(this, new AudioEndpointsChangedEventArgs());
        }
        catch (Exception exception)
        {
            await _logger.WriteAsync("error", "default-endpoint-event-failed", new { exception.Message })
                .ConfigureAwait(false);
        }
    }

    private void OnEndpointCatalogChanged(object? sender, AudioEndpointCatalogChangedEventArgs eventArgs) =>
        AudioEndpointsChanged?.Invoke(this, new AudioEndpointsChangedEventArgs());

    private async Task SetCurrentOutputCoreAsync(string endpointId, CancellationToken cancellationToken)
    {
        if (!await _audioController.IsEndpointUsableAsync(endpointId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The selected Windows audio output is unavailable.");
        }

        _ = await _orchestrator.HandleDefaultEndpointChangedAsync(
            AudioRole.Multimedia,
            endpointId,
            cancellationToken).ConfigureAwait(false);
        await _audioController.ApplyAsync(endpointId, AllRoles, cancellationToken).ConfigureAwait(false);
        if (!await _audioController.VerifyAsync(endpointId, AllRoles, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Windows did not apply the selected audio output.");
        }

        await _logger.WriteAsync(
            "information",
            "manual-audio-output-selected",
            new { endpointId },
            cancellationToken).ConfigureAwait(false);
        AudioEndpointsChanged?.Invoke(this, new AudioEndpointsChangedEventArgs());
    }

    private Task LogRoutingAsync(RoutingResult result, CancellationToken cancellationToken = default) =>
        _logger.WriteAsync(
            result.Outcome == RoutingOutcome.Failed ? "error" : "information",
            "routing-result",
            new { outcome = result.Outcome.ToString(), result.ErrorCode },
            cancellationToken);

    private async Task ActivateConfiguredRouteAsync(CancellationToken cancellationToken = default)
    {
        if (!Settings.AutomationEnabled || string.IsNullOrWhiteSpace(Settings.TargetEndpointId))
        {
            return;
        }

        var rule = new AutomationRule(
            Guid.Parse("ac06f88e-f63b-45c3-9835-2e058f4cf229"),
            "discord-voice",
            Settings.TargetEndpointId,
            100);
        var result = await _orchestrator.ActivateAsync(rule, cancellationToken).ConfigureAwait(false);
        RoutingStateChanged?.Invoke(this, new RoutingStateChangedEventArgs(result));
        await LogRoutingAsync(result, cancellationToken).ConfigureAwait(false);
    }

    private async Task RestoreAndPublishAsync(CancellationToken cancellationToken = default)
    {
        var result = await _orchestrator.RestoreAsync(cancellationToken).ConfigureAwait(false);
        RoutingStateChanged?.Invoke(this, new RoutingStateChangedEventArgs(result));
        await LogRoutingAsync(result, cancellationToken).ConfigureAwait(false);
    }

    private static VoicePresenceCoordinator CreateCoordinator(AppSettings settings) =>
        new(TimeSpan.FromSeconds(Math.Clamp(settings.ExitGracePeriodSeconds, 0, 30)));

    private void SetDiscordAuthorization(bool isAuthorized)
    {
        if (_isDiscordAuthorized == isAuthorized)
        {
            return;
        }

        _isDiscordAuthorized = isAuthorized;
        DiscordAuthorizationChanged?.Invoke(this, new DiscordAuthorizationChangedEventArgs(isAuthorized));
    }
}
