using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using Aeziol.App.Appearance;
using Aeziol.App.Localization;
using Aeziol.App.Services;
using Aeziol.App.Settings;
using Forms = System.Windows.Forms;

namespace Aeziol.App;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF owns the application lifecycle; resources are disposed in OnExit.")]
public partial class App : System.Windows.Application
{
    private const int UnpackagedActivationUnavailableHResult = unchecked((int)0xD0000225);

    private AeziolRuntime? _runtime;
    private AppLogger? _logger;
    private LocalizationService? _localization;
    private JsonAppSettingsStore? _settingsStore;
    private AppPaths? _paths;
    private Task? _runtimeInitialization;
    private AmbientMusicService? _ambientMusic;
    private SingleInstanceCoordinator? _singleInstance;
    private Forms.NotifyIcon? _trayIcon;
    private Icon? _trayIconImage;
    private Forms.ToolStripItem? _openTrayItem;
    private Forms.ToolStripItem? _quitTrayItem;
    private MainWindowSessionState? _mainWindowSessionState;
    private bool _showMainWindowWhenReady;
    private bool _runMainWindowStartupTasks = true;
    private bool _isUnloadingMainWindow;
    private bool _isQuitting;
    private readonly bool _suppressProductStartup;
    private int _handlingFatalException;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    internal App(bool suppressProductStartup)
        : this()
    {
        _suppressProductStartup = suppressProductStartup;
    }

    private void OnToggleSwitchClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not System.Windows.Controls.CheckBox toggle
            || toggle.Template.FindName("Thumb", toggle) is not System.Windows.Shapes.Ellipse thumb)
        {
            return;
        }

        thumb.BeginAnimation(FrameworkElement.MarginProperty, null);
        if (MotionAssist.GetIsReduced(toggle))
        {
            return;
        }

        var isChecked = toggle.IsChecked == true;
        thumb.BeginAnimation(
            FrameworkElement.MarginProperty,
            new ThicknessAnimation(
                isChecked ? new Thickness(3, 0, 0, 0) : new Thickness(23, 0, 0, 0),
                isChecked ? new Thickness(23, 0, 0, 0) : new Thickness(3, 0, 0, 0),
                TimeSpan.FromMilliseconds(isChecked ? 160 : 140))
            {
                FillBehavior = FillBehavior.Stop,
            });
    }

    private void OnDeviceCheckClick(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not System.Windows.Controls.CheckBox checkBox
            || checkBox.Template.FindName("Tick", checkBox) is not System.Windows.Shapes.Path tick
            || tick.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        tick.BeginAnimation(UIElement.OpacityProperty, null);
        if (MotionAssist.GetIsReduced(checkBox))
        {
            return;
        }

        if (scale.IsFrozen)
        {
            scale = scale.CloneCurrentValue();
            tick.RenderTransform = scale;
        }

        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        var isChecked = checkBox.IsChecked == true;
        var fromScale = isChecked ? 0.65 : 1;
        var toScale = isChecked ? 1 : 0.65;
        var duration = TimeSpan.FromMilliseconds(isChecked ? 140 : 80);
        scale.ScaleX = toScale;
        scale.ScaleY = toScale;
        tick.BeginAnimation(
            UIElement.OpacityProperty,
            new DoubleAnimation(isChecked ? 0 : 1, isChecked ? 1 : 0, duration) { FillBehavior = FillBehavior.Stop });
        scale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(fromScale, toScale, duration) { FillBehavior = FillBehavior.Stop });
        scale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(fromScale, toScale, duration) { FillBehavior = FillBehavior.Stop });
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (_suppressProductStartup)
        {
            return;
        }

        try
        {
            var isUiPreview = e.Args.Contains("--ui-preview", StringComparer.OrdinalIgnoreCase);
            var forceFirstRun = e.Args.Contains("--first-run", StringComparer.OrdinalIgnoreCase);
            var isPreviewBackground = isUiPreview
                && e.Args.Contains("--background", StringComparer.OrdinalIgnoreCase);
            var isPreviewMinimizeToTray = isUiPreview
                && e.Args.Contains("--minimize-to-tray", StringComparer.OrdinalIgnoreCase);
            var isWindowsStartup = isPreviewBackground || (!isUiPreview && IsWindowsStartup(e.Args));
            if (!isUiPreview)
            {
                _singleInstance = new SingleInstanceCoordinator();
                if (!_singleInstance.IsPrimaryInstance)
                {
                    _ = _singleInstance.SignalPrimaryInstance();
                    Shutdown();
                    return;
                }

                _singleInstance.ActivationRequested += OnSingleInstanceActivationRequested;
            }

            var paths = _paths = isUiPreview
                ? new AppPaths(Path.Combine(Path.GetTempPath(), "Aeziol.UiPreview"))
                : AppPaths.CreateDefault();
            var logger = _logger = new AppLogger(paths.LogsDirectory);
            _settingsStore = new JsonAppSettingsStore(paths.SettingsFile);
            AppSettings settings;
            if (isUiPreview)
            {
                settings = new AppSettings
                {
                    FirstRunCompleted = true,
                    AutomationEnabled = false,
                    Language = "fr",
                    CloseBehavior = isPreviewMinimizeToTray
                        ? CloseBehavior.MinimizeToTray
                        : CloseBehavior.Ask,
                    OpenHiddenAtWindowsStartup = isPreviewBackground,
                };
            }
            else
            {
                try
                {
                    settings = await _settingsStore.LoadAsync().ConfigureAwait(true);
                }
                catch (Exception exception)
                {
                    await logger.WriteAsync("error", "settings-load-failed", new { exception.Message }).ConfigureAwait(true);
                    throw new InvalidDataException(
                        "Aeziol n’a pas pu charger vos réglages. Le fichier a été conservé pour éviter toute perte.",
                        exception);
                }
            }

            var normalizedGracePeriod = GracePeriodOptions.Normalize(settings.ExitGracePeriodSeconds);
            if (!isUiPreview && normalizedGracePeriod != settings.ExitGracePeriodSeconds)
            {
                settings = settings with { ExitGracePeriodSeconds = normalizedGracePeriod };
                await _settingsStore.SaveAsync(settings).ConfigureAwait(true);
            }

            if (!isUiPreview && !settings.DiscordExecutableSearchCompleted)
            {
                settings = settings with
                {
                    DiscordExecutablePath = DiscordExecutableLocator.Find() ?? settings.DiscordExecutablePath,
                    DiscordExecutableSearchCompleted = true,
                };
                await _settingsStore.SaveAsync(settings).ConfigureAwait(true);
            }

            RenderOptions.ProcessRenderMode = settings.UseHardwareAcceleration
                ? RenderMode.Default
                : RenderMode.SoftwareOnly;

            AeziolThemeService.Apply(settings.Theme, settings.EnhanceContrast);
            _localization = new LocalizationService(
                Path.Combine(AppContext.BaseDirectory, "Localization"),
                paths.LanguagesDirectory,
                settings.Language);

            _ambientMusic = new AmbientMusicService(
                Path.Combine(AppContext.BaseDirectory, "Assets", "Audio", "onde-doree.mp3"));
            _ambientMusic.Apply(settings);
            if (forceFirstRun || !settings.FirstRunCompleted)
            {
                var previewMusicEnabled = settings.AmbientMusicEnabled;
                var previewMusicVolume = settings.AmbientMusicVolumePercent;
                _ambientMusic.SetApplicationVisible(true);
                var firstRun = new FirstRunWindow(
                    _localization,
                    settings.Language,
                    settings.Theme,
                    settings.EnhanceContrast,
                    settings.StartWithWindows,
                    settings.ReduceAnimations,
                    settings.AmbientMusicEnabled,
                    enabled =>
                    {
                        previewMusicEnabled = enabled;
                        _ambientMusic.Apply(settings with
                        {
                            AmbientMusicEnabled = previewMusicEnabled,
                            AmbientMusicVolumePercent = previewMusicVolume,
                        });
                    },
                    settings.AmbientMusicVolumePercent,
                    settings.KeepAmbientMusicPlayingWhenHidden,
                    settings.KeepAmbientMusicPlayingWhenUnfocused,
                    volume =>
                    {
                        previewMusicVolume = volume;
                        _ambientMusic.Apply(settings with
                        {
                            AmbientMusicEnabled = previewMusicEnabled,
                            AmbientMusicVolumePercent = previewMusicVolume,
                        });
                    });
                if (firstRun.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }

                settings = settings with
                {
                    FirstRunCompleted = true,
                    Language = firstRun.SelectedLanguage,
                    Theme = firstRun.SelectedTheme,
                    StartWithWindows = firstRun.StartWithWindows,
                    ReduceAnimations = firstRun.ReduceAnimations,
                    AmbientMusicEnabled = firstRun.AmbientMusicEnabled,
                    AmbientMusicVolumePercent = firstRun.AmbientMusicVolumePercent,
                    KeepAmbientMusicPlayingWhenHidden = firstRun.KeepAmbientMusicPlayingWhenHidden,
                    KeepAmbientMusicPlayingWhenUnfocused = firstRun.KeepAmbientMusicPlayingWhenUnfocused,
                };
                await _settingsStore.SaveAsync(settings).ConfigureAwait(true);
                await AutostartService.SetEnabledAsync(
                    settings.StartWithWindows,
                    settings.OpenHiddenAtWindowsStartup).ConfigureAwait(true);
            }

            _localization.ChangeLanguage(settings.Language);
            _ambientMusic.Apply(settings);
            _runtime = new AeziolRuntime(settings, paths, logger);
            _runtimeInitialization = _runtime.InitializeAsync();
            CreateTrayIcon();
            if (ShouldShowMainWindow(
                    _showMainWindowWhenReady,
                    isWindowsStartup,
                    settings.OpenHiddenAtWindowsStartup))
            {
                ShowMainWindow();
            }
            else
            {
                await _runtimeInitialization.ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            WriteUnhandledException("startup", exception);
            MessageBox.Show(exception.Message, "Aeziol", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        _ambientMusic?.SetApplicationFocused(true);
    }

    protected override void OnDeactivated(EventArgs e)
    {
        _ambientMusic?.SetApplicationFocused(false);
        base.OnDeactivated(e);
    }

    public void HandleMainWindowClosing(CancelEventArgs eventArgs)
    {
        if (_isQuitting
            || _isUnloadingMainWindow
            || MainWindow is not MainWindow window
            || _localization is null)
        {
            return;
        }

        if (window.CurrentSettings.CloseBehavior == CloseBehavior.Ask)
        {
            eventArgs.Cancel = true;
            window.ShowCloseActionsMenu();
            return;
        }

        if (window.CurrentSettings.CloseBehavior == CloseBehavior.MinimizeToTray)
        {
            PrepareMainWindowForBackground(window);
            return;
        }

        RequestQuit();
    }

    public void RequestQuit()
    {
        _isQuitting = true;
        Dispatcher.BeginInvoke(new Action(Shutdown));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        if (_singleInstance is not null)
        {
            _singleInstance.ActivationRequested -= OnSingleInstanceActivationRequested;
            _singleInstance.Dispose();
        }

        _trayIcon?.Dispose();
        _trayIconImage?.Dispose();
        _ambientMusic?.Dispose();
        if (_runtime is not null)
        {
            _runtime.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        else
        {
            _logger?.Dispose();
        }

        base.OnExit(e);
    }

    private void OnSingleInstanceActivationRequested(object? sender, EventArgs eventArgs) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (MainWindow is MainWindow)
            {
                ShowMainWindow();
                return;
            }

            var visibleWindow = Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsVisible);
            if (visibleWindow is not null)
            {
                visibleWindow.Activate();
                return;
            }

            if (_runtime is not null)
            {
                ShowMainWindow();
            }
            else
            {
                _showMainWindowWhenReady = true;
            }
        });

    private void OnDispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        if (Interlocked.Exchange(ref _handlingFatalException, 1) != 0)
        {
            return;
        }

        eventArgs.Handled = true;
        WriteUnhandledException("wpf-dispatcher", eventArgs.Exception);
        var title = _localization?.Get("unexpected-error-title", WritingRegister.Standard)
            ?? "Aeziol encountered an unexpected error";
        var message = _localization?.Get("unexpected-error-message", WritingRegister.Standard)
            ?? "Aeziol will close safely. The error was recorded in the local logs.";
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        RequestQuit();
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)
    {
        if (eventArgs.ExceptionObject is Exception exception)
        {
            WriteUnhandledException("app-domain", exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        WriteUnhandledException("unobserved-task", eventArgs.Exception);
        eventArgs.SetObserved();
    }

    private void WriteUnhandledException(string source, Exception exception)
    {
        try
        {
            _logger?.WriteAsync(
                "critical",
                "unhandled-exception",
                new
                {
                    source,
                    exceptionType = exception.GetType().FullName,
                    exception.Message,
                    stackTrace = exception.ToString(),
                }).GetAwaiter().GetResult();
        }
        catch (Exception) when (_logger is not null)
        {
            // A crash report must never replace the original failure with a logging failure.
        }
    }

    private void CreateTrayIcon()
    {
        var executablePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("The Aeziol executable path is unavailable.");
        using (var icon = Icon.ExtractAssociatedIcon(executablePath)
            ?? throw new InvalidOperationException("The Aeziol application icon is missing."))
        {
            _trayIconImage = (Icon)icon.Clone();
        }

        var menu = new Forms.ContextMenuStrip();
        _openTrayItem = menu.Items.Add(string.Empty, null, (_, _) => Dispatcher.Invoke(ShowMainWindow));
        _quitTrayItem = menu.Items.Add(string.Empty, null, (_, _) => Dispatcher.Invoke(RequestQuit));
        _trayIcon = new Forms.NotifyIcon
        {
            Text = "Aeziol",
            Icon = _trayIconImage,
            Visible = true,
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
        RefreshTrayLocalization();
    }

    public void RefreshTrayLocalization()
    {
        if (_localization is null || _openTrayItem is null || _quitTrayItem is null)
        {
            return;
        }

        _openTrayItem!.Text = _localization.Get("tray-open", WritingRegister.Standard);
        _quitTrayItem!.Text = _localization.Get("tray-quit", WritingRegister.Standard);
    }

    public void ApplyAmbientMusic(AppSettings settings) => _ambientMusic?.Apply(settings);

    public void SetAmbientMusicHostVisible(bool isVisible) =>
        _ambientMusic?.SetApplicationVisible(isVisible);

    public void HideMainWindow(MainWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (_isQuitting || _isUnloadingMainWindow || !ReferenceEquals(MainWindow, window))
        {
            return;
        }

        PrepareMainWindowForBackground(window);
        window.Close();
    }

    internal static bool ShouldShowMainWindow(
        bool activationRequested,
        bool isWindowsStartup,
        bool openHiddenAtWindowsStartup) =>
        activationRequested || !isWindowsStartup || !openHiddenAtWindowsStartup;

    private static bool IsWindowsStartup(IReadOnlyCollection<string> arguments) =>
        IsWindowsStartup(
            arguments,
            () => global::Windows.ApplicationModel.AppInstance.GetActivatedEventArgs()?.Kind
                == global::Windows.ApplicationModel.Activation.ActivationKind.StartupTask);

    internal static bool IsWindowsStartup(
        IReadOnlyCollection<string> arguments,
        Func<bool> isStartupActivation)
    {
        if (arguments.Contains("--background", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        try
        {
            return isStartupActivation();
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.Runtime.InteropServices.COMException exception)
            when (exception.HResult == UnpackagedActivationUnavailableHResult)
        {
            return false;
        }
    }

    private void PrepareMainWindowForBackground(MainWindow window)
    {
        _mainWindowSessionState = window.CaptureSessionState();
        _isUnloadingMainWindow = true;
        SetAmbientMusicHostVisible(false);
    }

    private void ShowMainWindow()
    {
        if (MainWindow is not MainWindow window)
        {
            var runtime = _runtime
                ?? throw new InvalidOperationException("The Aeziol runtime is unavailable.");
            var settingsStore = _settingsStore
                ?? throw new InvalidOperationException("The settings store is unavailable.");
            var localization = _localization
                ?? throw new InvalidOperationException("The localization service is unavailable.");
            var paths = _paths
                ?? throw new InvalidOperationException("The application paths are unavailable.");
            var runtimeInitialization = _runtimeInitialization
                ?? throw new InvalidOperationException("Runtime initialization is unavailable.");

            window = new MainWindow(
                runtime,
                settingsStore,
                localization,
                paths,
                runtimeInitialization,
                _runMainWindowStartupTasks,
                _mainWindowSessionState);
            _runMainWindowStartupTasks = false;
            window.Closed += OnMainWindowClosed;
            MainWindow = window;
        }

        window.Show();
        window.WindowState = WindowState.Normal;
        if (_mainWindowSessionState?.WasMaximized == true)
        {
            window.WindowState = WindowState.Maximized;
        }

        window.Activate();
    }

    private void OnMainWindowClosed(object? sender, EventArgs eventArgs)
    {
        if (sender is not MainWindow window)
        {
            return;
        }

        window.Closed -= OnMainWindowClosed;
        if (ReferenceEquals(MainWindow, window))
        {
            MainWindow = null;
        }

        _isUnloadingMainWindow = false;
        SetAmbientMusicHostVisible(false);
        ScheduleHiddenUiCollection();
        _ = _logger?.WriteAsync(
            "information",
            "main-window-unloaded",
            new
            {
                HasMainWindow = MainWindow is not null,
                WindowCount = Windows.Count,
            });
    }

    private void ScheduleHiddenUiCollection() =>
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                if (_isQuitting || MainWindow is not null)
                {
                    return;
                }

                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: false);
                ProcessMemoryTrimmer.TryTrimWorkingSet();
            }));
}
