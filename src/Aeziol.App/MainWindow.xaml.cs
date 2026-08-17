using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Aeziol.App.Appearance;
using Aeziol.App.Localization;
using Aeziol.App.Notifications;
using Aeziol.App.Services;
using Aeziol.App.Settings;
using Aeziol.Core.Models;
using Aeziol.Infrastructure.Discord.Rpc;

namespace Aeziol.App;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF owns the window lifecycle; disposable fields are released from OnClosed.")]
public partial class MainWindow : Window
{
    private const string ProjectUrl = "https://github.com/GaetanGrd/Aeziol";
    private readonly AeziolRuntime _runtime;
    private readonly JsonAppSettingsStore _settingsStore;
    private readonly LocalizationService _localization;
    private readonly AppPaths _paths;
    private readonly Task _runtimeInitialization;
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private readonly NotificationCenter _notifications = new();
    private List<EndpointChoice> _endpointChoices = [];
    private AudioRouteSnapshot? _currentRoute;
    private CancellationTokenSource? _endpointRefreshCancellation;
    private CancellationTokenSource? _authorizationCancellation;
    private CancellationTokenSource? _ambientMusicVolumeSaveCancellation;
    private System.Windows.Threading.DispatcherTimer? _authorizationTimer;
    private DateTimeOffset _authorizationStartedAt;
    private TaskCompletionSource<ModalDecision>? _modalCompletion;
    private bool _initializing = true;
    private bool _syncingControls;
    private bool _aboutMusicCoverFailed;
    private bool _settingsMusicCoverFailed;
    private bool? _lastDiscordAuthorizationState;
    private readonly ScaleTransform _closeActionsMenuScale = new(1, 1);

    public MainWindow(
        AeziolRuntime runtime,
        JsonAppSettingsStore settingsStore,
        LocalizationService localization,
        AppPaths paths,
        Task runtimeInitialization)
    {
        _runtime = runtime;
        _settingsStore = settingsStore;
        _localization = localization;
        _paths = paths;
        _runtimeInitialization = runtimeInitialization;
        InitializeComponent();
        CloseActionsMenu.LayoutTransform = _closeActionsMenuScale;
        NotificationItems.ItemsSource = _notifications.Items;
        MotionAssist.SetIsReduced(this, runtime.Settings.ReduceAnimations);
        SourceInitialized += (_, _) => NativeWindowAppearance.HideSystemBorder(this);

        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
        SizeChanged += OnWindowSizeChanged;
        StateChanged += OnWindowStateChanged;
        IsVisibleChanged += OnWindowVisibilityChanged;
        _runtime.VoiceStateChanged += OnVoiceStateChanged;
        _runtime.RoutingStateChanged += OnRoutingStateChanged;
        _runtime.DiscordAuthorizationChanged += OnDiscordAuthorizationChanged;
        _runtime.AudioEndpointsChanged += OnAudioEndpointsChanged;
    }

    public AppSettings CurrentSettings => _runtime.Settings;

    private async void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            LoadSettingsIntoControls(_runtime.Settings);
            ApplyLocalization();
            await _runtimeInitialization.ConfigureAwait(true);
            await RefreshEndpointsAsync(debounce: false).ConfigureAwait(true);
            _initializing = false;
            UpdateVoiceState(_runtime.VoiceState);
            UpdateAuthorizationState(_runtime.IsDiscordAuthorized);

            var recovery = await _runtime.InspectRecoveryAsync().ConfigureAwait(true);
            if (recovery is not null)
            {
                var decision = await ShowModalAsync(
                    _localization.Get("recovery-title", SelectedRegister),
                    _localization.Get("recovery-message", SelectedRegister),
                    _localization.Get("confirm", SelectedRegister)).ConfigureAwait(true);
                await _runtime.ResolveRecoveryAsync(decision == ModalDecision.Confirm).ConfigureAwait(true);
                await RefreshEndpointsAsync(debounce: false).ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            _initializing = false;
            await ShowErrorAsync("Aeziol", exception).ConfigureAwait(true);
        }
    }

    private void LoadSettingsIntoControls(AppSettings settings)
    {
        _syncingControls = true;
        try
        {
            RuleAutomationToggle.IsChecked = settings.AutomationEnabled;
            UpdateAutomationPresentation(settings.AutomationEnabled, animate: false);
            AutostartToggle.IsChecked = settings.StartWithWindows;
            EnhancedContrastToggle.IsChecked = settings.EnhanceContrast;
            ReduceAnimationsToggle.IsChecked = settings.ReduceAnimations;
            AmbientMusicToggle.IsChecked = settings.AmbientMusicEnabled;
            PauseAmbientMusicWhenUnfocusedToggle.IsChecked = settings.PauseAmbientMusicWhenUnfocused;
            HardwareAccelerationToggle.IsChecked = settings.UseHardwareAcceleration;
            DiscordExecutablePathTextBox.Text = settings.DiscordExecutablePath ?? string.Empty;
            RefreshLanguageChoices(settings.Language);
            SelectByTag(ThemeCombo, settings.Theme.ToString());
            SelectByTag(CloseBehaviorCombo, settings.CloseBehavior.ToString());
            SelectByTag(GracePeriodCombo, settings.ExitGracePeriodSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
            AmbientMusicVolumeSlider.Value = Math.Clamp(settings.AmbientMusicVolumePercent, 0, 100);
            UpdateDiscordExecutableControls();
            UpdateCloseBehaviorPreview();
            UpdateAmbientMusicControls();
        }
        finally
        {
            _syncingControls = false;
        }
    }

    private async Task RefreshEndpointsAsync(bool debounce)
    {
        _endpointRefreshCancellation?.Cancel();
        _endpointRefreshCancellation?.Dispose();
        _endpointRefreshCancellation = new CancellationTokenSource();
        var cancellationToken = _endpointRefreshCancellation.Token;
        try
        {
            if (debounce)
            {
                await Task.Delay(140, cancellationToken).ConfigureAwait(true);
            }

            var endpointsTask = _runtime.GetEndpointsAsync(cancellationToken);
            var routeTask = _runtime.GetCurrentRouteAsync(cancellationToken);
            await Task.WhenAll(endpointsTask, routeTask).ConfigureAwait(true);
            var endpoints = await endpointsTask.ConfigureAwait(true);
            _currentRoute = await routeTask.ConfigureAwait(true);

            var duplicateNames = endpoints
                .GroupBy(endpoint => endpoint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

            var choices = endpoints
                .Where(endpoint => endpoint.IsUsable)
                .OrderBy(endpoint => endpoint.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(endpoint => new EndpointChoice(
                    endpoint.Id,
                    endpoint.DisplayName,
                    duplicateNames.Contains(endpoint.DisplayName)
                        ? endpoint.InterfaceName ?? endpoint.ContainerId ?? string.Empty
                        : endpoint.InterfaceName ?? string.Empty,
                    _runtime.Settings.ExcludedEndpointIds.Contains(endpoint.Id)))
                .ToList();

            if (_runtime.Settings.TargetEndpointId is { Length: > 0 } targetId
                && choices.All(choice => !string.Equals(choice.Id, targetId, StringComparison.OrdinalIgnoreCase)))
            {
                var previous = _endpointChoices.FirstOrDefault(choice =>
                    string.Equals(choice.Id, targetId, StringComparison.OrdinalIgnoreCase));
                choices.Insert(0, new EndpointChoice(
                    targetId,
                    previous?.DisplayName ?? _localization.Get("unavailable-output", SelectedRegister),
                    _localization.Get("unavailable", SelectedRegister),
                    _runtime.Settings.ExcludedEndpointIds.Contains(targetId),
                    isUsable: false));
            }

            _endpointChoices = choices;
            var wasSyncing = _syncingControls;
            _syncingControls = true;
            try
            {
                PassageDestinationCombo.ItemsSource = choices;
                RuleDestinationCombo.ItemsSource = choices;
                CurrentOutputCombo.ItemsSource = choices;
                ExclusionsList.ItemsSource = choices;
                PassageDestinationCombo.SelectedValue = _runtime.Settings.TargetEndpointId;
                RuleDestinationCombo.SelectedValue = _runtime.Settings.TargetEndpointId;
            }
            finally
            {
                _syncingControls = wasSyncing;
            }
            UpdateRouteSummary();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void UpdateRouteSummary()
    {
        var currentId = _runtime.ActiveRouteTransaction?.Source.Get(AudioRole.Multimedia)
            ?? _currentRoute?.Get(AudioRole.Multimedia);
        var wasSyncing = _syncingControls;
        _syncingControls = true;
        CurrentOutputCombo.SelectedValue = currentId;
        _syncingControls = wasSyncing;

        var restoreId = _runtime.ActiveRouteTransaction?.Source.Get(AudioRole.Multimedia);
        RestoreOutputText.Text = restoreId is null
            ? _localization.Get("restore-output-idle", SelectedRegister)
            : ResolveEndpointName(restoreId, _localization.Get("unknown-output", SelectedRegister));
        ForceRestoreButton.Visibility = restoreId is null ? Visibility.Collapsed : Visibility.Visible;
        UpdateDestinationWarning();
    }

    private void UpdateDestinationWarning()
    {
        var currentId = _currentRoute?.Get(AudioRole.Multimedia);
        var targetId = _runtime.Settings.TargetEndpointId;
        var isIdentical = !string.IsNullOrWhiteSpace(currentId)
            && !string.IsNullOrWhiteSpace(targetId)
            && string.Equals(currentId, targetId, StringComparison.OrdinalIgnoreCase);
        var warning = _localization.Get("identical-output-warning", SelectedRegister);

        TargetHelpText.Text = _localization.Get(
            isIdentical ? "identical-output-warning" : "target-help",
            SelectedRegister);
        TargetHelpText.Foreground = (System.Windows.Media.Brush)FindResource(
            isIdentical ? "AeziolGold" : "AeziolMuted");
        RuleDestinationWarningText.Text = warning;
        RuleDestinationWarningText.Visibility = isIdentical ? Visibility.Visible : Visibility.Collapsed;
    }

    private string ResolveEndpointName(string? endpointId, string fallback)
    {
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            return fallback;
        }

        return _endpointChoices.FirstOrDefault(choice =>
            string.Equals(choice.Id, endpointId, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? fallback;
    }

    private async Task PersistSettingsAsync(
        Func<AppSettings, AppSettings> update,
        bool updateAutostart = false)
    {
        await _settingsGate.WaitAsync().ConfigureAwait(true);
        try
        {
            var previous = _runtime.Settings;
            var settings = update(previous);
            if (settings == previous)
            {
                return;
            }

            await _settingsStore.SaveAsync(settings).ConfigureAwait(true);
            if (updateAutostart && previous.StartWithWindows != settings.StartWithWindows)
            {
                await AutostartService.SetEnabledAsync(settings.StartWithWindows).ConfigureAwait(true);
            }

            await _runtime.UpdateSettingsAsync(settings).ConfigureAwait(true);
            if (System.Windows.Application.Current is App app)
            {
                app.ApplyAmbientMusic(settings);
                app.RefreshTrayLocalization();
            }

            UpdateSettingsSummaries();
        }
        finally
        {
            _settingsGate.Release();
        }
    }

    private async void OnAutomationChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls)
        {
            return;
        }

        var enabled = sender is System.Windows.Controls.CheckBox checkBox && checkBox.IsChecked == true;
        await SetAutomationEnabledAsync(enabled).ConfigureAwait(true);
    }

    private async void OnAutomationAction(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls)
        {
            return;
        }

        await SetAutomationEnabledAsync(!_runtime.Settings.AutomationEnabled).ConfigureAwait(true);
    }

    private async Task SetAutomationEnabledAsync(bool enabled)
    {
        var previous = _runtime.Settings.AutomationEnabled;
        _syncingControls = true;
        RuleAutomationToggle.IsChecked = enabled;
        _syncingControls = false;
        UpdateAutomationPresentation(enabled, animate: true);
        AutomationActionButton.IsEnabled = false;
        try
        {
            await PersistSettingsAsync(settings => settings with { AutomationEnabled = enabled }).ConfigureAwait(true);
            UpdateVoiceState(_runtime.VoiceState);
            await RefreshEndpointsAsync(debounce: false).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _syncingControls = true;
            RuleAutomationToggle.IsChecked = previous;
            _syncingControls = false;
            UpdateAutomationPresentation(previous, animate: false);
            await ShowErrorAsync(_localization.Get("settings", SelectedRegister), exception).ConfigureAwait(true);
        }
        finally
        {
            AutomationActionButton.IsEnabled = true;
        }
    }

    private void UpdateAutomationPresentation(bool enabled, bool animate)
    {
        var presentation = AutomationPresentation.For(enabled);
        AutomationActionButton.Content = _localization.Get(presentation.ActionLocalizationKey, SelectedRegister);
        AutomationActionButton.Style = (Style)FindResource(presentation.ButtonStyleKey);
        PassageAutomationContent.IsEnabled = presentation.ContentIsEnabled;

        var currentOpacity = PassageAutomationContent.Opacity;
        PassageAutomationContent.BeginAnimation(OpacityProperty, null);
        PassageAutomationContent.Opacity = presentation.ContentOpacity;
        if (animate && !_runtime.Settings.ReduceAnimations)
        {
            PassageAutomationContent.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(
                    currentOpacity,
                    presentation.ContentOpacity,
                    TimeSpan.FromMilliseconds(160))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop,
                });
            return;
        }
    }

    private async void OnDestinationChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls || sender is not System.Windows.Controls.ComboBox comboBox)
        {
            return;
        }

        var targetId = comboBox.SelectedValue as string;
        if (!DestinationSelectionPolicy.ShouldPersist(targetId, _runtime.Settings.TargetEndpointId))
        {
            return;
        }

        SyncSelectedDestination(targetId);
        try
        {
            await PersistSettingsAsync(settings => settings with { TargetEndpointId = targetId }).ConfigureAwait(true);
            UpdateRouteSummary();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("destination", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private void SyncSelectedDestination(string? targetId)
    {
        var wasSyncing = _syncingControls;
        _syncingControls = true;
        try
        {
            PassageDestinationCombo.SelectedValue = targetId;
            RuleDestinationCombo.SelectedValue = targetId;
        }
        finally
        {
            _syncingControls = wasSyncing;
        }
    }

    private async void OnCurrentOutputChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls || CurrentOutputCombo.SelectedValue is not string endpointId)
        {
            return;
        }

        var currentId = _currentRoute?.Get(AudioRole.Multimedia);
        if (string.Equals(currentId, endpointId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentOutputCombo.IsEnabled = false;
        try
        {
            await _runtime.SetCurrentOutputAsync(endpointId).ConfigureAwait(true);
            await RefreshEndpointsAsync(debounce: false).ConfigureAwait(true);
            ShowRoutingFeedback("current-output-changed", isError: false);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("current-output", SelectedRegister), exception).ConfigureAwait(true);
            await RefreshEndpointsAsync(debounce: false).ConfigureAwait(true);
        }
        finally
        {
            CurrentOutputCombo.IsEnabled = true;
        }
    }

    private async void OnForceRestore(object sender, RoutedEventArgs eventArgs)
    {
        if (_runtime.ActiveRouteTransaction is null)
        {
            UpdateRouteSummary();
            return;
        }

        ForceRestoreButton.IsEnabled = false;
        try
        {
            await _runtime.ForceRestoreAsync().ConfigureAwait(true);
            await RefreshEndpointsAsync(debounce: false).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("force-restore", SelectedRegister), exception).ConfigureAwait(true);
        }
        finally
        {
            ForceRestoreButton.IsEnabled = true;
            UpdateRouteSummary();
        }
    }

    private async void OnExclusionChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing)
        {
            return;
        }

        try
        {
            var exclusions = _endpointChoices
                .Where(choice => choice.IsExcluded)
                .Select(choice => choice.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            await PersistSettingsAsync(settings => settings with { ExcludedEndpointIds = exclusions }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("exclusions", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private async void OnLanguageChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls || LanguageList.SelectedValue is not string language)
        {
            return;
        }

        try
        {
            _localization.ChangeLanguage(language);
            ApplyLocalization();
            await PersistSettingsAsync(settings => settings with { Language = language }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync("Aeziol", exception).ConfigureAwait(true);
        }
    }

    private void RefreshLanguageChoices(string selectedLanguage)
    {
        var wasSyncing = _syncingControls;
        _syncingControls = true;
        try
        {
            LanguageList.ItemsSource = LanguageCardOptionFactory.Create(_localization);
            LanguageList.SelectedValue = selectedLanguage;
        }
        finally
        {
            _syncingControls = wasSyncing;
        }
    }

    private async void OnThemeChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls
            || !Enum.TryParse<AeziolTheme>(SelectedTag(ThemeCombo), out var theme))
        {
            return;
        }

        try
        {
            ApplyThemeAndRefresh(theme, EnhancedContrastToggle.IsChecked == true);
            await PersistSettingsAsync(settings => settings with { Theme = theme }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("theme", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private void ApplyThemeAndRefresh(AeziolTheme theme, bool enhanceContrast)
    {
        AeziolThemeService.Apply(theme, enhanceContrast);
        PassageJourneyTrace.RefreshPalette();
        SettingsJourneyTrace.RefreshPalette();
        ExclusionsJourneyTrace.RefreshPalette();
    }

    private async void OnEnhancedContrastChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls)
        {
            return;
        }

        try
        {
            var enabled = EnhancedContrastToggle.IsChecked == true;
            ApplyThemeAndRefresh(SelectedTheme, enabled);
            await PersistSettingsAsync(settings => settings with { EnhanceContrast = enabled }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("enhance-contrast", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private async void OnReduceAnimationsChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls)
        {
            return;
        }

        var reduced = ReduceAnimationsToggle.IsChecked == true;
        MotionAssist.SetIsReduced(this, reduced);
        UpdateMusicCovers();
        try
        {
            await PersistSettingsAsync(settings => settings with { ReduceAnimations = reduced }).ConfigureAwait(true);
            UpdateMusicCovers();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("reduce-animations", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private async void OnAmbientMusicEnabledChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls)
        {
            return;
        }

        try
        {
            var enabled = AmbientMusicToggle.IsChecked == true;
            await PersistSettingsAsync(settings => settings with { AmbientMusicEnabled = enabled }).ConfigureAwait(true);
            UpdateAmbientMusicControls();
        }
        catch (Exception exception)
        {
            UpdateAmbientMusicControls();
            await ShowErrorAsync(_localization.Get("ambient-music", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private async void OnAmbientMusicVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> eventArgs)
    {
        UpdateAmbientMusicVolumePreview();
        if (_initializing || _syncingControls)
        {
            return;
        }

        var volume = Math.Clamp((int)Math.Round(AmbientMusicVolumeSlider.Value), 0, 100);
        _ambientMusicVolumeSaveCancellation?.Cancel();
        _ambientMusicVolumeSaveCancellation?.Dispose();
        _ambientMusicVolumeSaveCancellation = new CancellationTokenSource();
        var cancellationToken = _ambientMusicVolumeSaveCancellation.Token;

        if (System.Windows.Application.Current is App app)
        {
            app.ApplyAmbientMusic(_runtime.Settings with { AmbientMusicVolumePercent = volume });
        }

        try
        {
            await Task.Delay(180, cancellationToken).ConfigureAwait(true);
            await PersistSettingsAsync(settings => settings with { AmbientMusicVolumePercent = volume }).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("ambient-music-volume", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private async void OnToggleAmbientMusicMute(object sender, RoutedEventArgs eventArgs)
    {
        var enabled = !_runtime.Settings.AmbientMusicEnabled;
        _syncingControls = true;
        AmbientMusicToggle.IsChecked = enabled;
        _syncingControls = false;

        try
        {
            await PersistSettingsAsync(settings => settings with { AmbientMusicEnabled = enabled }).ConfigureAwait(true);
            UpdateAmbientMusicControls();
        }
        catch (Exception exception)
        {
            _syncingControls = true;
            AmbientMusicToggle.IsChecked = !enabled;
            _syncingControls = false;
            await ShowErrorAsync(_localization.Get("ambient-music", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private async void OnPauseAmbientMusicWhenUnfocusedChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls)
        {
            return;
        }

        try
        {
            var pauseWhenUnfocused = PauseAmbientMusicWhenUnfocusedToggle.IsChecked == true;
            await PersistSettingsAsync(settings => settings with
            {
                PauseAmbientMusicWhenUnfocused = pauseWhenUnfocused,
            }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _syncingControls = true;
            PauseAmbientMusicWhenUnfocusedToggle.IsChecked = _runtime.Settings.PauseAmbientMusicWhenUnfocused;
            _syncingControls = false;
            await ShowErrorAsync(_localization.Get("ambient-music", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private async void OnHardwareAccelerationChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls)
        {
            return;
        }

        try
        {
            var enabled = HardwareAccelerationToggle.IsChecked == true;
            await PersistSettingsAsync(settings => settings with { UseHardwareAcceleration = enabled }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _syncingControls = true;
            HardwareAccelerationToggle.IsChecked = _runtime.Settings.UseHardwareAcceleration;
            _syncingControls = false;
            await ShowErrorAsync(_localization.Get("hardware-acceleration", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private async void OnResetSetting(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string setting })
        {
            return;
        }

        var defaults = new AppSettings();
        Func<AppSettings, AppSettings>? update = setting switch
        {
            "Language" => current => current with { Language = defaults.Language },
            "Theme" => current => current with { Theme = defaults.Theme },
            "EnhanceContrast" => current => current with { EnhanceContrast = defaults.EnhanceContrast },
            "ReduceAnimations" => current => current with { ReduceAnimations = defaults.ReduceAnimations },
            "CloseBehavior" => current => current with { CloseBehavior = defaults.CloseBehavior },
            "GracePeriod" => current => current with { ExitGracePeriodSeconds = defaults.ExitGracePeriodSeconds },
            "Autostart" => current => current with { StartWithWindows = defaults.StartWithWindows },
            "AmbientMusicEnabled" => current => current with { AmbientMusicEnabled = defaults.AmbientMusicEnabled },
            "AmbientMusicVolume" => current => current with { AmbientMusicVolumePercent = defaults.AmbientMusicVolumePercent },
            "PauseAmbientMusicWhenUnfocused" => current => current with
            {
                PauseAmbientMusicWhenUnfocused = defaults.PauseAmbientMusicWhenUnfocused,
            },
            "HardwareAcceleration" => current => current with
            {
                UseHardwareAcceleration = defaults.UseHardwareAcceleration,
            },
            _ => null,
        };
        if (update is null)
        {
            return;
        }

        try
        {
            if (setting == "AmbientMusicVolume")
            {
                _ambientMusicVolumeSaveCancellation?.Cancel();
            }

            await PersistSettingsAsync(update, updateAutostart: setting == "Autostart").ConfigureAwait(true);
            if (setting == "Language")
            {
                _localization.ChangeLanguage(_runtime.Settings.Language);
            }

            if (setting is "Theme" or "EnhanceContrast")
            {
                ApplyThemeAndRefresh(_runtime.Settings.Theme, _runtime.Settings.EnhanceContrast);
            }

            if (setting == "ReduceAnimations")
            {
                MotionAssist.SetIsReduced(this, _runtime.Settings.ReduceAnimations);
                UpdateMusicCovers();
            }

            LoadSettingsIntoControls(_runtime.Settings);
            ApplyLocalization();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("reset-setting", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private async void OnResetApplicationSettings(object sender, RoutedEventArgs eventArgs)
    {
        var decision = await ShowModalAsync(
            _localization.Get("reset-application-title", SelectedRegister),
            _localization.Get("reset-application-message", SelectedRegister),
            _localization.Get("reset-application-confirm", SelectedRegister),
            danger: true).ConfigureAwait(true);
        if (decision != ModalDecision.Confirm)
        {
            return;
        }

        try
        {
            _ambientMusicVolumeSaveCancellation?.Cancel();
            await PersistSettingsAsync(
                ApplicationSettingsDefaults.Reset,
                updateAutostart: _runtime.Settings.StartWithWindows).ConfigureAwait(true);
            _localization.ChangeLanguage(_runtime.Settings.Language);
            ApplyThemeAndRefresh(_runtime.Settings.Theme, _runtime.Settings.EnhanceContrast);
            MotionAssist.SetIsReduced(this, _runtime.Settings.ReduceAnimations);
            LoadSettingsIntoControls(_runtime.Settings);
            ApplyLocalization();
            UpdateMusicCovers();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("reset-application-title", SelectedRegister), exception)
                .ConfigureAwait(true);
        }
    }

    private void UpdateAmbientMusicControls()
    {
        if (!IsInitialized)
        {
            return;
        }

        var enabled = _runtime.Settings.AmbientMusicEnabled;
        if (AmbientMusicToggle.IsChecked != enabled)
        {
            _syncingControls = true;
            AmbientMusicToggle.IsChecked = enabled;
            _syncingControls = false;
        }

        AmbientMusicVolumeSlider.IsEnabled = enabled;
        AmbientMusicMutedGlyph.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;
        var label = _localization.Get(enabled ? "ambient-music-mute" : "ambient-music-unmute", SelectedRegister);
        AmbientMusicMuteButton.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(AmbientMusicMuteButton, label);
        UpdateAmbientMusicVolumePreview();
    }

    private void UpdateAmbientMusicVolumePreview()
    {
        if (!IsInitialized)
        {
            return;
        }

        var volume = Math.Clamp((int)Math.Round(AmbientMusicVolumeSlider.Value), 0, 100);
        AmbientMusicVolumeValueText.Text = $"{volume} %";
        AmbientMusicVolumeWarningText.Visibility = volume > 20 ? Visibility.Visible : Visibility.Collapsed;
        UpdateSettingsSummaries();
    }

    private async void OnAutostartChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls)
        {
            return;
        }

        try
        {
            var enabled = AutostartToggle.IsChecked == true;
            await PersistSettingsAsync(
                settings => settings with { StartWithWindows = enabled },
                updateAutostart: true).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("autostart", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private async void OnCloseBehaviorChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls
            || !Enum.TryParse<CloseBehavior>(SelectedTag(CloseBehaviorCombo), out var behavior))
        {
            return;
        }

        try
        {
            await PersistSettingsAsync(settings => settings with { CloseBehavior = behavior }).ConfigureAwait(true);
            UpdateCloseBehaviorPreview();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("close-behavior", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private async void OnGracePeriodChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (_initializing || _syncingControls
            || !int.TryParse(SelectedTag(GracePeriodCombo), out var seconds))
        {
            return;
        }

        try
        {
            await PersistSettingsAsync(settings => settings with { ExitGracePeriodSeconds = seconds }).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("grace-period", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private void OnBrowseDiscordExecutable(object sender, RoutedEventArgs eventArgs)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _localization.Get("discord-executable-picker", SelectedRegister),
            Filter = "Discord (Discord.exe)|Discord.exe|Executable (*.exe)|*.exe",
            CheckFileExists = true,
            Multiselect = false,
            InitialDirectory = ResolveDiscordExecutableInitialDirectory(),
        };
        if (dialog.ShowDialog(this) == true)
        {
            DiscordExecutablePathTextBox.Text = dialog.FileName;
            _ = PersistDiscordExecutablePathAsync(dialog.FileName);
        }
    }

    private void OnClearDiscordExecutable(object sender, RoutedEventArgs eventArgs)
    {
        DiscordExecutablePathTextBox.Text = string.Empty;
        _ = PersistDiscordExecutablePathAsync(null);
    }

    private async void OnAutoDetectDiscordExecutable(object sender, RoutedEventArgs eventArgs)
    {
        AutoDetectDiscordExecutableButton.IsEnabled = false;
        try
        {
            var detectedPath = await Task.Run(DiscordExecutableLocator.Find).ConfigureAwait(true);
            if (detectedPath is null)
            {
                await ShowModalAsync(
                    _localization.Get("discord-executable-not-found-title", SelectedRegister),
                    _localization.Get("discord-executable-not-found-message", SelectedRegister),
                    _localization.Get("confirm", SelectedRegister)).ConfigureAwait(true);
                return;
            }

            DiscordExecutablePathTextBox.Text = detectedPath;
            await PersistDiscordExecutablePathAsync(detectedPath).ConfigureAwait(true);
        }
        finally
        {
            AutoDetectDiscordExecutableButton.IsEnabled = true;
        }
    }

    private void OnDiscordExecutablePathLostFocus(object sender, RoutedEventArgs eventArgs)
    {
        if (!_initializing && !_syncingControls)
        {
            _ = PersistDiscordExecutablePathAsync(DiscordExecutablePathTextBox.Text);
        }
    }

    private void OnDiscordExecutablePathKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != System.Windows.Input.Key.Enter)
        {
            return;
        }

        eventArgs.Handled = true;
        _ = PersistDiscordExecutablePathAsync(DiscordExecutablePathTextBox.Text);
        System.Windows.Input.Keyboard.ClearFocus();
    }

    private async Task PersistDiscordExecutablePathAsync(string? value)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        string? normalized = null;
        if (trimmed is not null)
        {
            try
            {
                normalized = Path.GetFullPath(trimmed);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                await ShowDiscordExecutableErrorAsync().ConfigureAwait(true);
                return;
            }

            if (!File.Exists(normalized)
                || !string.Equals(Path.GetExtension(normalized), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                await ShowDiscordExecutableErrorAsync().ConfigureAwait(true);
                return;
            }
        }

        if (string.Equals(normalized, _runtime.Settings.DiscordExecutablePath, StringComparison.OrdinalIgnoreCase))
        {
            DiscordExecutablePathTextBox.Text = normalized ?? string.Empty;
            UpdateDiscordExecutableControls();
            return;
        }

        try
        {
            DiscordExecutablePathTextBox.IsEnabled = false;
            await PersistSettingsAsync(settings => settings with
            {
                DiscordExecutablePath = normalized,
                DiscordExecutableSearchCompleted = true,
            }).ConfigureAwait(true);
            DiscordExecutablePathTextBox.Text = normalized ?? string.Empty;
            UpdateDiscordExecutableControls();
        }
        catch (Exception exception)
        {
            DiscordExecutablePathTextBox.Text = _runtime.Settings.DiscordExecutablePath ?? string.Empty;
            await ShowErrorAsync(_localization.Get("discord-executable", SelectedRegister), exception).ConfigureAwait(true);
        }
        finally
        {
            DiscordExecutablePathTextBox.IsEnabled = true;
        }
    }

    private Task<ModalDecision> ShowDiscordExecutableErrorAsync() => ShowModalAsync(
        _localization.Get("discord-executable-invalid-title", SelectedRegister),
        _localization.Get("discord-executable-invalid-message", SelectedRegister),
        _localization.Get("confirm", SelectedRegister));

    private string? ResolveDiscordExecutableInitialDirectory()
    {
        var configured = _runtime.Settings.DiscordExecutablePath;
        if (configured is not null && Path.GetDirectoryName(configured) is { } directory && Directory.Exists(directory))
        {
            return directory;
        }

        return null;
    }

    private void UpdateDiscordExecutableControls()
    {
        var hasCustomPath = !string.IsNullOrWhiteSpace(DiscordExecutablePathTextBox.Text);
        ClearDiscordExecutableButton.Visibility = hasCustomPath ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnAuthorizeDiscord(object sender, RoutedEventArgs eventArgs)
    {
        if (_authorizationCancellation is not null)
        {
            return;
        }

        var authorizationCancellation = new CancellationTokenSource();
        _authorizationCancellation = authorizationCancellation;
        AuthorizeDiscordButton.IsEnabled = false;
        PassageDiscordActionButton.IsEnabled = false;
        AuthorizeDiscordButton.Content = _localization.Get("authorizing-discord", SelectedRegister);
        PassageDiscordActionButton.Content = _localization.Get("authorizing-discord", SelectedRegister);
        BeginAuthorizationWait();
        try
        {
            if (!await _runtime.AuthorizeDiscordAsync(authorizationCancellation.Token).ConfigureAwait(true))
            {
                ShowRoutingFeedback("authorization-failed", isError: true);
            }
        }
        catch (OperationCanceledException) when (authorizationCancellation.IsCancellationRequested)
        {
            ShowRoutingFeedback("authorization-cancelled", isError: false);
        }
        catch (Exception exception)
        {
            await ShowAuthorizationErrorAsync(exception).ConfigureAwait(true);
        }
        finally
        {
            EndAuthorizationWait();
            authorizationCancellation.Dispose();
            if (ReferenceEquals(_authorizationCancellation, authorizationCancellation))
            {
                _authorizationCancellation = null;
            }
            AuthorizeDiscordButton.IsEnabled = true;
            PassageDiscordActionButton.IsEnabled = true;
            UpdateAuthorizationState(_runtime.IsDiscordAuthorized);
        }
    }

    private void OnCancelAuthorization(object sender, RoutedEventArgs eventArgs) =>
        _authorizationCancellation?.Cancel();

    private void BeginAuthorizationWait()
    {
        _authorizationStartedAt = DateTimeOffset.UtcNow;
        UpdateAuthorizationElapsed();
        AuthorizationLayer.Visibility = Visibility.Visible;
        _authorizationTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _authorizationTimer.Tick += OnAuthorizationTimerTick;
        _authorizationTimer.Start();
    }

    private void EndAuthorizationWait()
    {
        if (_authorizationTimer is not null)
        {
            _authorizationTimer.Stop();
            _authorizationTimer.Tick -= OnAuthorizationTimerTick;
            _authorizationTimer = null;
        }

        AuthorizationLayer.Visibility = Visibility.Collapsed;
    }

    private void OnAuthorizationTimerTick(object? sender, EventArgs eventArgs) => UpdateAuthorizationElapsed();

    private void UpdateAuthorizationElapsed()
    {
        var elapsed = DateTimeOffset.UtcNow - _authorizationStartedAt;
        AuthorizationElapsedText.Text = elapsed.ToString(@"m\:ss", System.Globalization.CultureInfo.InvariantCulture);
    }

    private void OnPassageDiscordAction(object sender, RoutedEventArgs eventArgs)
        => OnAuthorizeDiscord(sender, eventArgs);

    private async void OnRequestRevokeDiscord(object sender, RoutedEventArgs eventArgs)
    {
        var decision = await ShowModalAsync(
            _localization.Get("revoke-discord-title", SelectedRegister),
            _localization.Get("revoke-discord-message", SelectedRegister),
            _localization.Get("revoke-discord-confirm", SelectedRegister),
            danger: true).ConfigureAwait(true);
        if (decision != ModalDecision.Confirm)
        {
            return;
        }

        try
        {
            await _runtime.RevokeDiscordAuthorizationAsync().ConfigureAwait(true);
            ShowRoutingFeedback("discord-revoked", isError: false);
        }
        catch (Exception exception)
        {
            var fallback = await ShowModalAsync(
                _localization.Get("revoke-discord-failed-title", SelectedRegister),
                _localization.Get("revoke-discord-failed-message", SelectedRegister),
                _localization.Get("retry", SelectedRegister),
                _localization.Get("forget-local", SelectedRegister),
                exception.Message).ConfigureAwait(true);
            if (fallback == ModalDecision.Secondary)
            {
                await _runtime.ForgetDiscordAuthorizationAsync().ConfigureAwait(true);
                ShowRoutingFeedback("discord-forgotten", isError: false);
            }
        }
        finally
        {
            UpdateAuthorizationState(_runtime.IsDiscordAuthorized);
            await RefreshEndpointsAsync(debounce: false).ConfigureAwait(true);
        }
    }

    private async Task ShowAuthorizationErrorAsync(Exception exception)
    {
        var key = exception switch
        {
            DiscordRpcException { Code: 4007 } => "authorization-error-client-id",
            DiscordRpcException rpc when rpc.Message.Contains("401", StringComparison.Ordinal) =>
                "authorization-error-public-client",
            HttpRequestException => "authorization-error-network",
            _ => "authorization-error-rpc",
        };
        await ShowModalAsync(
            _localization.Get("authorization-failed", SelectedRegister),
            _localization.Get(key, SelectedRegister),
            _localization.Get("confirm", SelectedRegister),
            technicalDetail: SafeTechnicalDetail(exception)).ConfigureAwait(true);
    }

    private async Task ShowErrorAsync(string title, Exception exception)
    {
        await ShowModalAsync(
            title,
            _localization.Get("generic-error", SelectedRegister),
            _localization.Get("confirm", SelectedRegister),
            technicalDetail: SafeTechnicalDetail(exception)).ConfigureAwait(true);
    }

    private static string SafeTechnicalDetail(Exception exception)
    {
        var detail = exception.Message.ReplaceLineEndings(" ").Trim();
        return detail.Length <= 400 ? detail : detail[..400] + "…";
    }

    private Task<ModalDecision> ShowModalAsync(
        string title,
        string message,
        string confirmText,
        string? secondaryText = null,
        string? technicalDetail = null,
        bool danger = false)
    {
        _modalCompletion?.TrySetResult(ModalDecision.Cancel);
        _modalCompletion = new TaskCompletionSource<ModalDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        ModalTitleText.Text = title;
        ModalMessageText.Text = message;
        ModalConfirmButton.Content = confirmText;
        ModalConfirmButton.Style = (Style)FindResource(danger ? "DangerButton" : "PrimaryButton");
        ModalConfirmButton.Visibility = Visibility.Visible;
        ModalCancelButton.Content = _localization.Get("cancel", SelectedRegister);
        ModalSecondaryButton.Content = secondaryText;
        ModalSecondaryButton.Visibility = string.IsNullOrWhiteSpace(secondaryText)
            ? Visibility.Collapsed
            : Visibility.Visible;
        ModalTechnicalText.Text = technicalDetail;
        ModalTechnicalText.Visibility = string.IsNullOrWhiteSpace(technicalDetail)
            ? Visibility.Collapsed
            : Visibility.Visible;
        ModalLayer.Visibility = Visibility.Visible;
        ModalConfirmButton.Focus();
        return _modalCompletion.Task;
    }

    private void CompleteModal(ModalDecision decision)
    {
        ModalLayer.Visibility = Visibility.Collapsed;
        var completion = _modalCompletion;
        _modalCompletion = null;
        completion?.TrySetResult(decision);
    }

    private void OnModalConfirm(object sender, RoutedEventArgs eventArgs) => CompleteModal(ModalDecision.Confirm);

    private void OnModalSecondary(object sender, RoutedEventArgs eventArgs) => CompleteModal(ModalDecision.Secondary);

    private void OnModalCancel(object sender, RoutedEventArgs eventArgs) => CompleteModal(ModalDecision.Cancel);

    private void OnVoiceStateChanged(object? sender, VoiceStateChangedEventArgs eventArgs) =>
        Dispatcher.BeginInvoke(() => UpdateVoiceState(eventArgs.State));

    private void OnRoutingStateChanged(object? sender, RoutingStateChangedEventArgs eventArgs) =>
        Dispatcher.BeginInvoke(async () =>
        {
            UpdateRoutingState(eventArgs.Result);
            await RefreshEndpointsAsync(debounce: true).ConfigureAwait(true);
        });

    private void OnDiscordAuthorizationChanged(object? sender, DiscordAuthorizationChangedEventArgs eventArgs) =>
        Dispatcher.BeginInvoke(() => UpdateAuthorizationState(eventArgs.IsAuthorized));

    private void OnAudioEndpointsChanged(object? sender, AudioEndpointsChangedEventArgs eventArgs) =>
        Dispatcher.BeginInvoke(async () => await RefreshEndpointsAsync(debounce: true).ConfigureAwait(true));

    private void UpdateVoiceState(VoicePresenceState state)
    {
        var key = state switch
        {
            VoicePresenceState.DiscordAbsent => "status-discord-absent",
            VoicePresenceState.OutOfVoice => "status-out-of-voice",
            VoicePresenceState.Connecting or VoicePresenceState.ChangingChannel or VoicePresenceState.Reconnecting =>
                "status-connecting",
            VoicePresenceState.Connected => "status-connected",
            VoicePresenceState.AuthorizationRequired => "status-authorization-required",
            VoicePresenceState.Unavailable => "status-unavailable",
            _ => "status-disconnected",
        };
        var text = _localization.Get(key, SelectedRegister);
        VoicePillText.Text = text;
        DiscordSourceStateText.Text = text;

        var active = state == VoicePresenceState.Connected;
        var waiting = state is VoicePresenceState.Connecting
            or VoicePresenceState.ChangingChannel
            or VoicePresenceState.Reconnecting;
        var brush = (System.Windows.Media.Brush)FindResource(active
            ? "AeziolSuccess"
            : waiting
                ? "AeziolGold"
                : "AeziolDim");
        VoicePillDot.Fill = brush;
        RailStatusDot.Fill = brush;
        RailStatusText.Text = _runtime.Settings.AutomationEnabled
            ? _localization.Get(active ? "active" : "watching", SelectedRegister)
            : _localization.Get("paused", SelectedRegister);
    }

    private void UpdateRoutingState(RoutingResult result)
    {
        if (result.Outcome == RoutingOutcome.NoChangeNeeded)
        {
            return;
        }

        var key = result.Outcome switch
        {
            RoutingOutcome.Applied => "routing-applied",
            RoutingOutcome.Retargeted => "routing-retargeted",
            RoutingOutcome.Restored => "routing-restored",
            RoutingOutcome.SkippedByExclusion => "routing-excluded",
            RoutingOutcome.TargetUnavailable => "routing-target-unavailable",
            RoutingOutcome.SourceUnavailable => "routing-source-unavailable",
            RoutingOutcome.RolledBack => "routing-rolled-back",
            RoutingOutcome.AbandonedByUser => "routing-manual-change",
            RoutingOutcome.RecoveryConfirmationRequired => "routing-recovery-required",
            _ => "routing-failed",
        };
        ShowRoutingFeedback(
            key,
            result.Outcome is RoutingOutcome.TargetUnavailable
                or RoutingOutcome.SourceUnavailable
                or RoutingOutcome.Failed,
            isWarning: result.Outcome == RoutingOutcome.Retargeted);
    }

    private void ShowRoutingFeedback(string localizationKey, bool isError, bool isWarning = false)
    {
        var severity = isError
            ? NotificationSeverity.Error
            : isWarning
                ? NotificationSeverity.Warning
                : NotificationSeverity.Success;
        var notification = _notifications.Publish(
            _localization.Get(localizationKey, SelectedRegister),
            severity);
        NotificationHost.Visibility = Visibility.Visible;
        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = notification.DisplayDuration,
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            DismissNotification(notification.Id);
        };
        timer.Start();
    }

    private void OnDismissNotification(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is System.Windows.Controls.Button { Tag: Guid id })
        {
            DismissNotification(id);
        }
    }

    private void DismissNotification(Guid id)
    {
        _notifications.Dismiss(id);
        NotificationHost.Visibility = _notifications.Items.Count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void OnNotificationCardLoaded(object sender, RoutedEventArgs eventArgs)
    {
        if (_runtime.Settings.ReduceAnimations || sender is not Border card)
        {
            return;
        }

        var translation = new TranslateTransform(18, 0);
        card.RenderTransform = translation;
        card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        });
        translation.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        });
    }

    private void UpdateAuthorizationState(bool isAuthorized)
    {
        AuthorizeDiscordButton.Visibility = isAuthorized ? Visibility.Collapsed : Visibility.Visible;
        AuthorizeDiscordButton.Content = _localization.Get("authorize-discord", SelectedRegister);
        RevokeDiscordButton.Visibility = isAuthorized ? Visibility.Visible : Visibility.Collapsed;
        RevokeDiscordButton.Content = _localization.Get("revoke-discord", SelectedRegister);
        PassageDiscordActionButton.Visibility = isAuthorized ? Visibility.Collapsed : Visibility.Visible;
        PassageDiscordActionButton.Content = _localization.Get("authorize-discord", SelectedRegister);
        PassageDiscordActionButton.Style = (Style)FindResource("PrimaryButton");
        var authorizationText = _localization.Get(
            isAuthorized ? "discord-authorized" : "discord-not-authorized",
            SelectedRegister);
        var authorizationBrush = (System.Windows.Media.Brush)FindResource(
            isAuthorized ? "AeziolSuccess" : "AeziolMuted");
        DiscordAuthorizationStateText.Text = authorizationText;
        DiscordAuthorizationStateText.Foreground = authorizationBrush;
        DiscordRouteStateDot.Fill = authorizationBrush;
        DiscordConnectedTrailCanvas.Visibility = isAuthorized ? Visibility.Visible : Visibility.Collapsed;
        DiscordBrokenTrailCanvas.Visibility = isAuthorized ? Visibility.Collapsed : Visibility.Visible;
        DiscordConnectedTrailCanvas.BeginAnimation(OpacityProperty, null);
        DiscordBrokenTrailCanvas.BeginAnimation(OpacityProperty, null);
        DiscordRuptureGlints.BeginAnimation(OpacityProperty, null);
        DiscordBrokenTrailCanvas.Opacity = 0.76;
        DiscordRuptureGlints.Opacity = 0.76;

        if (!isAuthorized
            && _lastDiscordAuthorizationState is true
            && !_runtime.Settings.ReduceAnimations)
        {
            DiscordBrokenTrailCanvas.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 0.76, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                });

            var glintPulse = new DoubleAnimationUsingKeyFrames();
            glintPulse.KeyFrames.Add(new DiscreteDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            glintPulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(150)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
            });
            glintPulse.KeyFrames.Add(new EasingDoubleKeyFrame(0.76, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
            });
            DiscordRuptureGlints.BeginAnimation(OpacityProperty, glintPulse);
        }

        _lastDiscordAuthorizationState = isAuthorized;
    }

    private void UpdateCloseBehaviorPreview()
    {
        var behavior = Enum.TryParse<CloseBehavior>(SelectedTag(CloseBehaviorCombo), out var selected)
            ? selected
            : _runtime.Settings.CloseBehavior;
        var key = behavior switch
        {
            CloseBehavior.Ask => "close-preview-ask",
            CloseBehavior.MinimizeToTray => "close-preview-tray",
            CloseBehavior.Quit => "close-preview-quit",
            _ => "close-preview-ask",
        };
        CloseBehaviorPreviewText.Text = _localization.Get(key, SelectedRegister);
    }

    private void ApplyLocalization()
    {
        var register = SelectedRegister;
        Title = _localization.Get("window-title", register);
        FlowDirection = _localization.IsRightToLeft
            ? System.Windows.FlowDirection.RightToLeft
            : System.Windows.FlowDirection.LeftToRight;

        PassageNavText.Text = _localization.Get("nav-passage", register);
        RulesNavText.Text = _localization.Get("nav-rules", register);
        SettingsNavText.Text = _localization.Get("nav-settings", register);
        PassageTitleText.Text = _localization.Get("page-passage-title", register);
        PassageSubtitleText.Text = _localization.Get("page-passage-subtitle", register);
        UpdateAutomationPresentation(_runtime.Settings.AutomationEnabled, animate: false);
        SourceLabelText.Text = _localization.Get("source", register);
        TargetLabelText.Text = _localization.Get("destination", register);
        PassageDestinationPlaceholderText.Text = _localization.Get("choose-output", register);
        TargetHelpText.Text = _localization.Get("target-help", register);
        CurrentOutputLabelText.Text = _localization.Get("current-output", register);
        RestoreOutputLabelText.Text = _localization.Get("restore-output", register);
        ForceRestoreButton.Content = _localization.Get("force-restore", register);

        RulesTitleText.Text = _localization.Get("page-rules-title", register);
        RulesSubtitleText.Text = _localization.Get("page-rules-subtitle", register);
        RuleTriggerLabelText.Text = _localization.Get("rule-trigger", register);
        RuleNameText.Text = _localization.Get("rule-name", register);
        RuleWhenLabelText.Text = _localization.Get("rule-when-label", register);
        RuleWhenText.Text = _localization.Get("rule-when", register);
        RulePriorityLabelText.Text = _localization.Get("rule-priority", register);
        RuleFutureText.Text = _localization.Get("rule-future", register);
        RuleDestinationLabelText.Text = _localization.Get("rule-destination", register);
        ExclusionsTitleText.Text = _localization.Get("exclusions", register);
        ExclusionsHelpText.Text = _localization.Get("exclusions-help", register);
        UpdateDestinationWarning();

        SettingsTitleText.Text = _localization.Get("page-settings-title", register);
        SettingsSubtitleText.Text = _localization.Get("page-settings-subtitle", register);
        SettingsGeneralTab.Content = _localization.Get("settings-section-general", register);
        SettingsDiscordTab.Content = _localization.Get("settings-section-discord", register);
        DiscordSettingsTitleText.Text = _localization.Get("discord-connection", register);
        DiscordRouteCaptionText.Text = _localization.Get("discord-route-caption", register);
        DiscordFallbackTitleText.Text = _localization.Get("discord-fallback-title", register);
        DiscordFallbackHintText.Text = _localization.Get("discord-fallback-hint", register);
        AuthorizeDiscordButton.Content = _localization.Get("authorize-discord", register);
        InterfaceSectionText.Text = _localization.Get("interface-section", register);
        BehaviorSectionText.Text = _localization.Get("behavior-section", register);
        LanguageRowText.Text = _localization.Get("language", register);
        ThemeRowText.Text = _localization.Get("theme", register);
        CloseBehaviorRowText.Text = _localization.Get("lifecycle-settings", register);
        MusicRowText.Text = _localization.Get("ambient-music", register);
        ApplicationRowText.Text = _localization.Get("maintenance", register);
        LanguageLabelText.Text = _localization.Get("language", register);
        RefreshLanguageChoices(_localization.CurrentLanguage);
        ThemeLabelText.Text = _localization.Get("theme", register);
        ThemePreviewText.Text = _localization.Get("theme-preview", register);
        EnhancedContrastToggle.Content = _localization.Get("enhance-contrast", register);
        ReduceAnimationsToggle.Content = _localization.Get("reduce-animations", register);
        AmbientMusicToggle.Content = _localization.Get("ambient-music-enabled", register);
        AmbientMusicTrackTitleText.Text = _localization.Get("about-music-title", register);
        AmbientMusicTrackCreditText.Text = _localization.Get("ambient-music-credit", register);
        AmbientMusicVolumeLabelText.Text = _localization.Get("ambient-music-volume", register);
        AmbientMusicRecommendedText.Text = _localization.Get("ambient-music-recommended", register);
        AmbientMusicMaximumText.Text = _localization.Get("ambient-music-maximum", register);
        AmbientMusicVolumeWarningText.Text = _localization.Get("ambient-music-loud-warning", register);
        AmbientMusicHelpText.Text = _localization.Get("ambient-music-pending", register);
        PauseAmbientMusicWhenUnfocusedText.Text = _localization.Get("ambient-music-pause-unfocused", register);
        HardwareAccelerationText.Text = _localization.Get("hardware-acceleration", register);
        HardwareAccelerationHintText.Text = _localization.Get("hardware-acceleration-restart", register);
        var resetSettingLabel = _localization.Get("reset-setting", register);
        var resetButtonStyle = (Style)FindResource("SettingsHoverResetButton");
        foreach (var resetButton in FindVisualChildren<System.Windows.Controls.Button>(SettingsView)
                     .Where(button => ReferenceEquals(button.Style, resetButtonStyle)))
        {
            resetButton.ToolTip = resetSettingLabel;
            System.Windows.Automation.AutomationProperties.SetName(resetButton, resetSettingLabel);
        }
        AutostartToggle.Content = _localization.Get("autostart", register);
        DiscordExecutableLabelText.Text = _localization.Get("discord-executable", register);
        DiscordExecutableHelpText.Text = _localization.Get("discord-executable-help", register);
        BrowseDiscordExecutableButton.Content = _localization.Get("browse", register);
        AutoDetectDiscordExecutableButton.Content = _localization.Get("discord-executable-auto", register);
        CloseBehaviorLabelText.Text = _localization.Get("close-behavior", register);
        ((ComboBoxItem)CloseBehaviorCombo.Items[0]).Content = _localization.Get("close-choice-ask", register);
        ((ComboBoxItem)CloseBehaviorCombo.Items[1]).Content = _localization.Get("close-choice-tray", register);
        ((ComboBoxItem)CloseBehaviorCombo.Items[2]).Content = _localization.Get("close-choice-quit", register);
        GracePeriodLabelText.Text = _localization.Get("grace-period", register);
        GracePeriodHelpText.Text = _localization.Get("grace-period-help", register);
        for (var index = 0; index < GracePeriodCombo.Items.Count; index++)
        {
            if (GracePeriodCombo.Items[index] is ComboBoxItem item && item.Tag is { } tag)
            {
                item.Content = _localization.Get("grace-" + tag, register);
            }
        }

        ReplayOnboardingButton.Content = _localization.Get("replay-onboarding", register);
        OpenLogsButton.Content = _localization.Get("logs", register);
        ResetApplicationSettingsButton.Content = _localization.Get("reset-application", register);
        AuthorizationWaitingTitleText.Text = _localization.Get("authorization-waiting-title", register);
        AuthorizationWaitingMessageText.Text = _localization.Get("authorization-waiting-message", register);
        AuthorizationWaitingHintText.Text = _localization.Get("authorization-waiting-hint", register);
        CancelAuthorizationButton.Content = _localization.Get("authorization-cancel", register);
        var version = "Aeziol " + ApplicationVersion.Current;
        VersionText.Text = version;
        AboutVersionText.Text = version;
        var aboutLabel = _localization.Get("about-tooltip", register);
        AboutButton.ToolTip = aboutLabel;
        System.Windows.Automation.AutomationProperties.SetName(AboutButton, aboutLabel);
        var minimizeLabel = _localization.Get("window-minimize", register);
        MinimizeButton.ToolTip = minimizeLabel;
        System.Windows.Automation.AutomationProperties.SetName(MinimizeButton, minimizeLabel);
        var closeLabel = _localization.Get("window-close", register);
        CloseWindowButton.ToolTip = closeLabel;
        System.Windows.Automation.AutomationProperties.SetName(CloseWindowButton, closeLabel);
        CloseHideMenuText.Text = _localization.Get("close-choice-hide-compact", register);
        CloseQuitMenuText.Text = _localization.Get("close-choice-quit-compact", register);
        UpdateWindowStateVisuals();
        AboutTitleText.Text = _localization.Get("about-title", register);
        AboutSubtitleText.Text = _localization.Get("about-subtitle", register);
        AboutAuthorLabelText.Text = _localization.Get("about-author-label", register);
        AboutAuthorText.Text = _localization.Get("about-author", register);
        AboutAiText.Text = _localization.Get("about-ai", register);
        AboutMusicSectionLabelText.Text = _localization.Get("about-music-section-label", register);
        AboutMusicPurposeText.Text = _localization.Get("about-music-purpose", register);
        AboutMusicTitleText.Text = _localization.Get("about-music-title", register);
        AboutMusicCreditText.Text = _localization.Get("about-music-credit", register);
        AboutElgoText.Text = _localization.Get("about-elgo", register);
        AboutLoreText.Text = _localization.Get("about-lore", register);
        AboutDismissButton.Content = _localization.Get("about-close", register);
        AboutGitHubButton.Content = _localization.Get("about-github", register);
        UpdateAuthorizationState(_runtime.IsDiscordAuthorized);
        UpdateCloseBehaviorPreview();
        UpdateDiscordExecutableControls();
        UpdateAmbientMusicControls();
        UpdateVoiceState(_runtime.VoiceState);
        UpdateRouteSummary();
        UpdateNavigationContext();
        UpdateSettingsSummaries();
    }

    private void OnNavigatePassage(object sender, RoutedEventArgs eventArgs)
    {
        if (!IsInitialized)
        {
            return;
        }

        PassageView.Visibility = Visibility.Visible;
        RulesView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        UpdateSettingsMusicCover();
        UpdateNavigationContext();
    }

    private void OnNavigateRules(object sender, RoutedEventArgs eventArgs)
    {
        if (!IsInitialized)
        {
            return;
        }

        PassageView.Visibility = Visibility.Collapsed;
        RulesView.Visibility = Visibility.Visible;
        SettingsView.Visibility = Visibility.Collapsed;
        UpdateSettingsMusicCover();
        UpdateNavigationContext();
    }

    private void OnNavigateSettings(object sender, RoutedEventArgs eventArgs)
    {
        if (!IsInitialized)
        {
            return;
        }

        PassageView.Visibility = Visibility.Collapsed;
        RulesView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Visible;
        UpdateSettingsMusicCover();
        UpdateNavigationContext();
    }

    private void OnSettingsSectionChanged(object sender, RoutedEventArgs eventArgs)
    {
        if (!IsInitialized)
        {
            return;
        }

        var showDiscord = ReferenceEquals(sender, SettingsDiscordTab);
        HideSettingsEditor();
        SettingsGeneralPanel.Visibility = showDiscord ? Visibility.Collapsed : Visibility.Visible;
        SettingsDiscordPanel.Visibility = showDiscord ? Visibility.Visible : Visibility.Collapsed;
        if (showDiscord)
        {
            SettingsDiscordScrollViewer.ScrollToTop();
        }
        else
        {
            SettingsGeneralDetailsScrollViewer.ScrollToTop();
        }

        var panel = showDiscord ? SettingsDiscordPanel : SettingsGeneralPanel;
        AnimateSettingsPanel(panel);
        UpdateSettingsMusicCover();
    }

    private void OnSettingsJourneyEnter(
        object sender,
        System.Windows.Input.MouseEventArgs eventArgs)
    {
        if (sender is not FrameworkElement row || row.ActualHeight <= 0)
        {
            return;
        }

        var top = row.TranslatePoint(new System.Windows.Point(0, 0), SettingsJourneyTrace).Y;
        var fadeTop = Math.Max(0, top - 7);
        var fadeBottom = Math.Min(SettingsJourneyTrace.Height, top + row.ActualHeight + 7);
        var highlightRect = new Rect(
            0,
            fadeTop,
            SettingsJourneyTrace.Width,
            fadeBottom - fadeTop);
        SettingsJourneyTrace.ShowHighlight(row, highlightRect, MotionAssist.GetIsReduced(this));
    }

    private void OnSettingsJourneyLeave(
        object sender,
        System.Windows.Input.MouseEventArgs eventArgs) =>
        SettingsJourneyTrace.HideHighlight(sender, MotionAssist.GetIsReduced(this));

    private void OnPassageJourneySourceEnter(
        object sender,
        System.Windows.Input.MouseEventArgs eventArgs) => ShowPassageJourneyHighlight(sender, 0, 112);

    private void OnPassageJourneyTargetEnter(
        object sender,
        System.Windows.Input.MouseEventArgs eventArgs) => ShowPassageJourneyHighlight(sender, 88, 112);

    private void OnPassageJourneyLeave(
        object sender,
        System.Windows.Input.MouseEventArgs eventArgs) =>
        PassageJourneyTrace.HideHighlight(sender, MotionAssist.GetIsReduced(this));

    private void ShowPassageJourneyHighlight(object owner, double left, double width)
    {
        var right = Math.Min(PassageJourneyTrace.Width, left + width);
        PassageJourneyTrace.ShowHighlight(
            owner,
            new Rect(left, 0, right - left, PassageJourneyTrace.Height),
            MotionAssist.GetIsReduced(this));
    }

    private void OnExclusionJourneyEnter(
        object sender,
        System.Windows.Input.MouseEventArgs eventArgs)
    {
        if (sender is not FrameworkElement row
            || row.ActualHeight <= 0
            || ExclusionsJourneyHost.ActualHeight <= 0)
        {
            return;
        }

        var scale = ExclusionsJourneyTrace.Height / ExclusionsJourneyHost.ActualHeight;
        var rowTop = row.TranslatePoint(new System.Windows.Point(0, 0), ExclusionsJourneyHost).Y * scale;
        var fadeTop = Math.Max(0, rowTop - 5);
        var fadeBottom = Math.Min(
            ExclusionsJourneyTrace.Height,
            rowTop + (row.ActualHeight * scale) + 5);
        if (fadeBottom <= fadeTop)
        {
            return;
        }

        ExclusionsJourneyTrace.ShowHighlight(
            row,
            new Rect(0, fadeTop, ExclusionsJourneyTrace.Width, fadeBottom - fadeTop),
            MotionAssist.GetIsReduced(this));
    }

    private void OnExclusionJourneyLeave(
        object sender,
        System.Windows.Input.MouseEventArgs eventArgs) =>
        ExclusionsJourneyTrace.HideHighlight(sender, MotionAssist.GetIsReduced(this));

    private void OnOpenSettingsEditor(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string editorKey })
        {
            return;
        }

        LanguageEditorPanel.Visibility = Visibility.Collapsed;
        ThemeEditorPanel.Visibility = Visibility.Collapsed;
        BehaviorEditorPanel.Visibility = Visibility.Collapsed;
        MusicEditorPanel.Visibility = Visibility.Collapsed;
        MaintenanceEditorPanel.Visibility = Visibility.Collapsed;

        FrameworkElement editor;
        var titleKey = editorKey switch
        {
            "Language" => "language",
            "Theme" => "theme",
            "Behavior" => "lifecycle-settings",
            "Music" => "ambient-music",
            _ => "maintenance",
        };
        editor = editorKey switch
        {
            "Language" => LanguageEditorPanel,
            "Theme" => ThemeEditorPanel,
            "Behavior" => BehaviorEditorPanel,
            "Music" => MusicEditorPanel,
            _ => MaintenanceEditorPanel,
        };

        editor.Visibility = Visibility.Visible;
        SettingsEditorTitleText.Text = _localization.Get(titleKey, SelectedRegister);
        SettingsEditorScrollViewer.ScrollToTop();
        SettingsEditorLayer.Visibility = Visibility.Visible;
        SettingsEditorSurface.BeginAnimation(OpacityProperty, null);
        SettingsEditorSurface.RenderTransform = Transform.Identity;
        SettingsEditorSurface.Opacity = 1;

        if (!MotionAssist.GetIsReduced(this))
        {
            var translation = new TranslateTransform(18, 0);
            SettingsEditorSurface.RenderTransform = translation;
            SettingsEditorSurface.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                });
            translation.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(170))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                });
        }

        SettingsEditorCloseButton.Focus();
        UpdateSettingsMusicCover();
        eventArgs.Handled = true;
    }

    private void OnCloseSettingsEditor(object sender, RoutedEventArgs eventArgs)
    {
        HideSettingsEditor();
        eventArgs.Handled = true;
    }

    private void OnSettingsEditorBackdropClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.OriginalSource, SettingsEditorLayer))
        {
            HideSettingsEditor();
            eventArgs.Handled = true;
        }
    }

    private void OnSettingsEditorSurfaceClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs eventArgs) => eventArgs.Handled = true;

    private void HideSettingsEditor()
    {
        if (!IsInitialized || SettingsEditorLayer.Visibility != Visibility.Visible)
        {
            return;
        }

        SettingsMusicAnimatedCover.Stop();
        SettingsEditorLayer.Visibility = Visibility.Collapsed;
        SettingsEditorSurface.BeginAnimation(OpacityProperty, null);
        SettingsEditorSurface.RenderTransform = Transform.Identity;
        SettingsEditorSurface.Opacity = 1;
        UpdateSettingsMusicCover();
    }

    private void UpdateSettingsSummaries()
    {
        if (!IsInitialized)
        {
            return;
        }

        LanguageSummaryText.Text = (LanguageList.SelectedItem as LanguageCardOption)?.NativeName
            ?? _localization.CurrentLanguage.ToUpperInvariant();
        ThemeSummaryText.Text = SelectedTag(ThemeCombo) ?? _runtime.Settings.Theme.ToString();

        var closeBehavior = (CloseBehaviorCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? _runtime.Settings.CloseBehavior.ToString();
        var gracePeriod = (GracePeriodCombo.SelectedItem as ComboBoxItem)?.Content?.ToString()
            ?? $"{_runtime.Settings.ExitGracePeriodSeconds} s";
        BehaviorSummaryText.Text = $"{closeBehavior} · {gracePeriod}";

        var musicEnabled = AmbientMusicToggle.IsChecked == true;
        var volume = Math.Clamp((int)Math.Round(AmbientMusicVolumeSlider.Value), 0, 100);
        MusicSummaryText.Text = $"{_localization.Get(musicEnabled ? "active" : "paused", SelectedRegister)} · {volume} %";
        ApplicationSummaryText.Text = ApplicationVersion.Current;
    }

    private void AnimateSettingsPanel(FrameworkElement panel)
    {
        panel.BeginAnimation(OpacityProperty, null);
        panel.Opacity = 1;
        if (!MotionAssist.GetIsReduced(this))
        {
            panel.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                });
        }
    }

    private void UpdateNavigationContext()
    {
        WindowContextText.Text = _localization.Get(
            SettingsView.Visibility == Visibility.Visible
                ? "nav-settings"
                : RulesView.Visibility == Visibility.Visible
                    ? "nav-rules"
                    : "nav-passage",
            SelectedRegister);
    }

    private async void OnReplayOnboarding(object sender, RoutedEventArgs eventArgs)
    {
        var onboarding = new FirstRunWindow(
            _localization,
            _runtime.Settings.Language,
            _runtime.Settings.Theme,
            _runtime.Settings.EnhanceContrast,
            _runtime.Settings.StartWithWindows,
            _runtime.Settings.ReduceAnimations)
        {
            Owner = this,
        };
        if (onboarding.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _localization.ChangeLanguage(onboarding.SelectedLanguage);
            MotionAssist.SetIsReduced(this, onboarding.ReduceAnimations);
            await PersistSettingsAsync(
                settings => settings with
                {
                    FirstRunCompleted = true,
                    Language = onboarding.SelectedLanguage,
                    Theme = onboarding.SelectedTheme,
                    StartWithWindows = onboarding.StartWithWindows,
                    ReduceAnimations = onboarding.ReduceAnimations,
                },
                updateAutostart: true).ConfigureAwait(true);
            LoadSettingsIntoControls(_runtime.Settings);
            ApplyLocalization();
        }
        catch (Exception exception)
        {
            await ShowErrorAsync(_localization.Get("settings", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private void OnOpenLogs(object sender, RoutedEventArgs eventArgs)
    {
        Directory.CreateDirectory(_paths.LogsDirectory);
        Process.Start(new ProcessStartInfo { FileName = _paths.LogsDirectory, UseShellExecute = true });
    }

    private void OnOpenAbout(object sender, RoutedEventArgs eventArgs)
    {
        AboutLayer.Visibility = Visibility.Visible;
        UpdateMusicCovers();
        AboutCloseButton.Focus();
    }

    private void OnCloseAbout(object sender, RoutedEventArgs eventArgs) => HideAbout();

    private void OnAboutBackdropClick(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.OriginalSource, AboutLayer))
        {
            HideAbout();
            eventArgs.Handled = true;
        }
    }

    private void OnAboutDialogClick(object sender, System.Windows.Input.MouseButtonEventArgs eventArgs) =>
        eventArgs.Handled = true;

    private void UpdateAboutMusicCover()
    {
        var videoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "onde-doree-cover.mp4");
        var animate = AboutLayer.Visibility == Visibility.Visible
            && IsVisible
            && WindowState != WindowState.Minimized
            && !MotionAssist.GetIsReduced(this)
            && !_aboutMusicCoverFailed
            && File.Exists(videoPath);
        AboutMusicAnimatedCover.Visibility = animate ? Visibility.Visible : Visibility.Collapsed;
        AboutMusicStaticCover.Visibility = animate ? Visibility.Collapsed : Visibility.Visible;
        if (!animate)
        {
            AboutMusicAnimatedCover.Stop();
            return;
        }

        AboutMusicAnimatedCover.Source ??= new Uri(videoPath, UriKind.Absolute);
        AboutMusicAnimatedCover.Position = TimeSpan.Zero;
        AboutMusicAnimatedCover.Play();
    }

    private void UpdateSettingsMusicCover()
    {
        var videoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "onde-doree-cover.mp4");
        var animate = SettingsView.Visibility == Visibility.Visible
            && SettingsGeneralPanel.Visibility == Visibility.Visible
            && SettingsEditorLayer.Visibility == Visibility.Visible
            && MusicEditorPanel.Visibility == Visibility.Visible
            && AboutLayer.Visibility != Visibility.Visible
            && IsVisible
            && WindowState != WindowState.Minimized
            && !MotionAssist.GetIsReduced(this)
            && !_settingsMusicCoverFailed
            && File.Exists(videoPath);
        var wasAnimating = SettingsMusicAnimatedCover.Visibility == Visibility.Visible;
        SettingsMusicAnimatedCover.Visibility = animate ? Visibility.Visible : Visibility.Collapsed;
        SettingsMusicStaticCover.Visibility = animate ? Visibility.Collapsed : Visibility.Visible;
        if (!animate)
        {
            SettingsMusicAnimatedCover.Stop();
            return;
        }

        SettingsMusicAnimatedCover.Source ??= new Uri(videoPath, UriKind.Absolute);
        if (!wasAnimating)
        {
            SettingsMusicAnimatedCover.Position = TimeSpan.Zero;
            SettingsMusicAnimatedCover.Play();
        }
    }

    private void UpdateMusicCovers()
    {
        UpdateSettingsMusicCover();
        UpdateAboutMusicCover();
    }

    private void OnSettingsMusicCoverEnded(object sender, RoutedEventArgs eventArgs)
    {
        if (SettingsView.Visibility == Visibility.Visible
            && SettingsGeneralPanel.Visibility == Visibility.Visible
            && SettingsEditorLayer.Visibility == Visibility.Visible
            && MusicEditorPanel.Visibility == Visibility.Visible
            && AboutLayer.Visibility != Visibility.Visible
            && IsVisible
            && WindowState != WindowState.Minimized
            && !MotionAssist.GetIsReduced(this))
        {
            SettingsMusicAnimatedCover.Position = TimeSpan.Zero;
            SettingsMusicAnimatedCover.Play();
        }
    }

    private void OnSettingsMusicCoverFailed(object sender, ExceptionRoutedEventArgs eventArgs)
    {
        _settingsMusicCoverFailed = true;
        SettingsMusicAnimatedCover.Visibility = Visibility.Collapsed;
        SettingsMusicStaticCover.Visibility = Visibility.Visible;
    }

    private void OnAboutMusicCoverEnded(object sender, RoutedEventArgs eventArgs)
    {
        if (AboutLayer.Visibility == Visibility.Visible && !MotionAssist.GetIsReduced(this))
        {
            AboutMusicAnimatedCover.Position = TimeSpan.Zero;
            AboutMusicAnimatedCover.Play();
        }
    }

    private void OnAboutMusicCoverFailed(object sender, ExceptionRoutedEventArgs eventArgs)
    {
        _aboutMusicCoverFailed = true;
        AboutMusicAnimatedCover.Visibility = Visibility.Collapsed;
        AboutMusicStaticCover.Visibility = Visibility.Visible;
    }

    private void HideAbout()
    {
        AboutMusicAnimatedCover.Stop();
        AboutLayer.Visibility = Visibility.Collapsed;
        UpdateSettingsMusicCover();
    }

    private async void OnOpenProject(object sender, RoutedEventArgs eventArgs)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = ProjectUrl, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            HideAbout();
            await ShowErrorAsync(_localization.Get("about-title", SelectedRegister), exception).ConfigureAwait(true);
        }
    }

    private void OnWindowPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != System.Windows.Input.Key.Escape)
        {
            return;
        }

        if (AboutLayer.Visibility == Visibility.Visible)
        {
            HideAbout();
            eventArgs.Handled = true;
        }
        else if (SettingsEditorLayer.Visibility == Visibility.Visible)
        {
            HideSettingsEditor();
            eventArgs.Handled = true;
        }
    }

    private void OnMinimizeWindow(object sender, RoutedEventArgs eventArgs) => WindowState = WindowState.Minimized;

    private void OnToggleMaximizeWindow(object sender, RoutedEventArgs eventArgs) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnWindowStateChanged(object? sender, EventArgs eventArgs)
    {
        UpdateWindowStateVisuals();
        UpdateMusicCovers();
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs eventArgs) => UpdateResponsiveScale();

    private void UpdateResponsiveScale()
    {
        var width = WindowFrame.ActualWidth > 0 ? WindowFrame.ActualWidth : ActualWidth;
        var height = WindowFrame.ActualHeight > 0 ? WindowFrame.ActualHeight : ActualHeight;
        var scale = ResponsiveLayout.Scale(width, height);
        ResponsiveSurface.Width = width / scale;
        ResponsiveSurface.Height = height / scale;
        PageHost.Margin = new Thickness(
            ResponsiveLayout.FixedPhysicalLength(30, width, height),
            ResponsiveLayout.FixedPhysicalLength(24, width, height),
            ResponsiveLayout.FixedPhysicalLength(30, width, height),
            ResponsiveLayout.FixedPhysicalLength(28, width, height));
        _closeActionsMenuScale.ScaleX = scale;
        _closeActionsMenuScale.ScaleY = scale;
        PassageJourneyTrace.DisplayScale = scale;
        SettingsJourneyTrace.DisplayScale = scale;
        ExclusionsJourneyTrace.DisplayScale = scale;
    }

    private void OnWindowVisibilityChanged(object sender, DependencyPropertyChangedEventArgs eventArgs) =>
        UpdateMusicCovers();

    private void UpdateWindowStateVisuals()
    {
        if (!IsInitialized)
        {
            return;
        }

        var isMaximized = WindowState == WindowState.Maximized;
        WindowFrame.CornerRadius = isMaximized ? new CornerRadius(0) : new CornerRadius(14);
        UpdateResponsiveScale();
        MaximizeGlyph.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreGlyph.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;
        var label = _localization.Get(isMaximized ? "window-restore" : "window-maximize", SelectedRegister);
        MaximizeButton.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(MaximizeButton, label);
    }

    private void OnCloseWindow(object sender, RoutedEventArgs eventArgs)
    {
        CloseActionsMenu.PlacementTarget = CloseWindowButton;
        CloseActionsMenu.Placement = PlacementMode.Custom;
        CloseActionsMenu.CustomPopupPlacementCallback = PlaceCloseActionsMenu;
        CloseActionsMenu.IsOpen = true;
    }

    private static CustomPopupPlacement[] PlaceCloseActionsMenu(
        System.Windows.Size popupSize,
        System.Windows.Size targetSize,
        System.Windows.Point offset)
    {
        var position = ResponsiveLayout.RightAlignedPopupPosition(
            popupSize.Width,
            targetSize.Width,
            targetSize.Height);
        return
        [
            new CustomPopupPlacement(
                new System.Windows.Point(position.X + offset.X, position.Y + offset.Y),
                PopupPrimaryAxis.Horizontal),
        ];
    }

    private void OnHideFromCloseMenu(object sender, RoutedEventArgs eventArgs) => Hide();

    private void OnQuitFromCloseMenu(object sender, RoutedEventArgs eventArgs)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.RequestQuit();
        }
    }

    private void OnClosing(object? sender, CancelEventArgs eventArgs)
    {
        if (AboutLayer.Visibility == Visibility.Visible)
        {
            HideAbout();
            eventArgs.Cancel = true;
            return;
        }

        if (ModalLayer.Visibility == Visibility.Visible)
        {
            CompleteModal(ModalDecision.Cancel);
            eventArgs.Cancel = true;
            return;
        }

        if (System.Windows.Application.Current is App app)
        {
            app.HandleMainWindowClosing(eventArgs);
        }
    }

    private void OnClosed(object? sender, EventArgs eventArgs)
    {
        _authorizationCancellation?.Cancel();
        EndAuthorizationWait();
        _endpointRefreshCancellation?.Cancel();
        _endpointRefreshCancellation?.Dispose();
        _ambientMusicVolumeSaveCancellation?.Cancel();
        _ambientMusicVolumeSaveCancellation?.Dispose();
        SettingsMusicAnimatedCover.Stop();
        AboutMusicAnimatedCover.Stop();
        _settingsGate.Dispose();
        _runtime.VoiceStateChanged -= OnVoiceStateChanged;
        _runtime.RoutingStateChanged -= OnRoutingStateChanged;
        _runtime.DiscordAuthorizationChanged -= OnDiscordAuthorizationChanged;
        _runtime.AudioEndpointsChanged -= OnAudioEndpointsChanged;
        StateChanged -= OnWindowStateChanged;
        IsVisibleChanged -= OnWindowVisibilityChanged;
    }

    private static WritingRegister SelectedRegister => WritingRegister.Standard;

    private AeziolTheme SelectedTheme =>
        Enum.TryParse<AeziolTheme>(SelectedTag(ThemeCombo), out var theme)
            ? theme
            : _runtime.Settings.Theme;

    private static string? SelectedTag(System.Windows.Controls.ComboBox comboBox) =>
        (comboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();

    private static void SelectByTag(System.Windows.Controls.ComboBox comboBox, string tag) =>
        comboBox.SelectedItem = comboBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private enum ModalDecision
    {
        Cancel,
        Confirm,
        Secondary,
    }

    private sealed class EndpointChoice : INotifyPropertyChanged
    {
        private bool _isExcluded;

        public EndpointChoice(
            string id,
            string displayName,
            string detail,
            bool isExcluded,
            bool isUsable = true)
        {
            Id = id;
            DisplayName = displayName;
            Detail = detail;
            _isExcluded = isExcluded;
            IsUsable = isUsable;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Id { get; }

        public string DisplayName { get; }

        public string Detail { get; }

        public bool IsUsable { get; }

        public override string ToString() => DisplayName;

        public bool IsExcluded
        {
            get => _isExcluded;
            set
            {
                if (_isExcluded == value)
                {
                    return;
                }

                _isExcluded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExcluded)));
            }
        }
    }
}
