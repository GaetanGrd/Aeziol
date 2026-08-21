using System.Windows;
using System.Windows.Controls;
using Aeziol.App.Localization;
using Aeziol.App.Appearance;
using Aeziol.App.Settings;

namespace Aeziol.App;

public partial class FirstRunWindow : Window
{
    private readonly LocalizationService _localization;
    private readonly AeziolTheme _initialTheme;
    private readonly bool _enhanceContrast;
    private readonly Action<bool>? _ambientMusicEnabledChanged;
    private readonly Action<int>? _ambientMusicVolumeChanged;
    private bool _accepted;
    private bool _initializing = true;
    private bool _syncingLanguageChoices;

    public FirstRunWindow(
        LocalizationService localization,
        string language,
        AeziolTheme theme,
        bool enhanceContrast,
        bool startWithWindows = false,
        bool reduceAnimations = false,
        bool ambientMusicEnabled = false,
        Action<bool>? ambientMusicEnabledChanged = null,
        int ambientMusicVolumePercent = 8,
        bool keepAmbientMusicPlayingWhenHidden = false,
        bool pauseAmbientMusicWhenUnfocused = true,
        Action<int>? ambientMusicVolumeChanged = null)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _initialTheme = theme;
        _enhanceContrast = enhanceContrast;
        _ambientMusicEnabledChanged = ambientMusicEnabledChanged;
        _ambientMusicVolumeChanged = ambientMusicVolumeChanged;
        InitializeComponent();
        SourceInitialized += (_, _) => NativeWindowAppearance.HideSystemBorder(this);
        Activated += OnWindowActivated;
        Deactivated += OnWindowDeactivated;
        Closed += OnWindowClosed;
        RefreshLanguageChoices(language);
        FirstRunThemeCombo.SelectedItem = FirstRunThemeCombo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), theme.ToString(), StringComparison.OrdinalIgnoreCase))
            ?? FirstRunThemeCombo.Items[0];
        AutostartCheck.IsChecked = startWithWindows;
        ReduceAnimationsCheck.IsChecked = reduceAnimations;
        MusicEnabledCheck.IsChecked = ambientMusicEnabled;
        MusicVolumeSlider.Value = Math.Clamp(ambientMusicVolumePercent, 0, 100);
        KeepMusicPlayingWhenHiddenCheck.IsChecked = keepAmbientMusicPlayingWhenHidden;
        PauseMusicWhenUnfocusedCheck.IsChecked = pauseAmbientMusicWhenUnfocused;
        UpdateMusicControls();
        MotionAssist.SetIsReduced(this, reduceAnimations);
        ApplyLocalization();
        _initializing = false;
    }

    public string SelectedLanguage => FirstRunLanguageList.SelectedValue as string ?? "en";
    public bool StartWithWindows => AutostartCheck.IsChecked == true;
    public bool ReduceAnimations => ReduceAnimationsCheck.IsChecked == true;
    public bool AmbientMusicEnabled => MusicEnabledCheck.IsChecked == true;
    public int AmbientMusicVolumePercent => Math.Clamp((int)Math.Round(MusicVolumeSlider.Value), 0, 100);
    public bool KeepAmbientMusicPlayingWhenHidden => KeepMusicPlayingWhenHiddenCheck.IsChecked == true;
    public bool PauseAmbientMusicWhenUnfocused => PauseMusicWhenUnfocusedCheck.IsChecked == true;
    public AeziolTheme SelectedTheme =>
        Enum.TryParse<AeziolTheme>((FirstRunThemeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var theme)
            ? theme
            : _initialTheme;

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (!IsInitialized || _syncingLanguageChoices || FirstRunLanguageList.SelectedValue is not string language)
        {
            return;
        }

        _localization.ChangeLanguage(language);
        ApplyLocalization();
    }

    private void RefreshLanguageChoices(string selectedLanguage)
    {
        _syncingLanguageChoices = true;
        try
        {
            FirstRunLanguageList.ItemsSource = LanguageCardOptionFactory.Create(_localization);
            FirstRunLanguageList.SelectedValue = selectedLanguage;
        }
        finally
        {
            _syncingLanguageChoices = false;
        }
    }

    private void ApplyLocalization()
    {
        const WritingRegister register = WritingRegister.Standard;
        Title = _localization.Get("first-run-title", register);
        WelcomeTitleText.Text = _localization.Get("first-run-title", register);
        FlowDirection = _localization.IsRightToLeft
            ? System.Windows.FlowDirection.RightToLeft
            : System.Windows.FlowDirection.LeftToRight;
        WelcomeHelpText.Text = _localization.Get("first-run-help", register);
        LanguageLabel.Text = _localization.Get("language", register);
        RefreshLanguageChoices(_localization.CurrentLanguage);
        AutostartCheck.Content = _localization.Get("autostart", register);
        ReduceAnimationsCheck.Content = _localization.Get("reduce-animations", register);
        LocalDataHintText.Text = _localization.Get("first-run-local-data", register);
        ContinueButton.Content = _localization.Get("continue", register);
        MusicTitleText.Text = _localization.Get("first-run-music-title", register);
        MusicHelpText.Text = _localization.Get("first-run-music-help", register);
        MusicTrackTitleText.Text = _localization.Get("about-music-title", register);
        MusicCreditText.Text = _localization.Get("ambient-music-credit", register);
        MusicPurposeText.Text = _localization.Get("first-run-music-purpose", register);
        MusicEnabledCheck.Content = _localization.Get("first-run-music-enable", register);
        MusicVolumeLabelText.Text = _localization.Get("ambient-music-volume", register);
        PauseMusicWhenUnfocusedCheck.Content = _localization.Get("ambient-music-pause-unfocused", register);
        KeepMusicPlayingWhenHiddenCheck.Content = _localization.Get("ambient-music-keep-playing-hidden", register);
        MusicFocusPrecedenceText.Text = _localization.Get("ambient-music-focus-precedence", register);
        MusicChoiceHintText.Text = _localization.Get("first-run-music-choice", register);
        MusicBackButton.Content = _localization.Get("back", register);
        MusicContinueButton.Content = _localization.Get("continue", register);
        PaletteTitleText.Text = _localization.Get("first-run-theme-title", register);
        PaletteHelpText.Text = _localization.Get("first-run-theme-help", register);
        PaletteLabelText.Text = _localization.Get("theme", register);
        PalettePreviewText.Text = _localization.Get("first-run-theme-preview", register);
        PalettePreviewButtonText.Text = _localization.Get("continue", register);
        BackButton.Content = _localization.Get("back", register);
        FinishButton.Content = _localization.Get("finish", register);
        var closeLabel = _localization.Get("window-close", register);
        FirstRunCloseButton.ToolTip = closeLabel;
        System.Windows.Automation.AutomationProperties.SetName(FirstRunCloseButton, closeLabel);
    }

    private void OnReduceAnimationsChanged(object sender, RoutedEventArgs eventArgs) =>
        MotionAssist.SetIsReduced(this, ReduceAnimationsCheck.IsChecked == true);

    private static void OnWindowActivated(object? sender, EventArgs eventArgs)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.SetAmbientMusicHostFocused(true);
        }
    }

    private static void OnWindowDeactivated(object? sender, EventArgs eventArgs)
    {
        if (System.Windows.Application.Current is App app)
        {
            app.SetAmbientMusicHostFocused(false);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs eventArgs)
    {
        OnWindowDeactivated(sender, eventArgs);
        Activated -= OnWindowActivated;
        Deactivated -= OnWindowDeactivated;
        Closed -= OnWindowClosed;
    }

    private void OnMusicEnabledChanged(object sender, RoutedEventArgs eventArgs)
    {
        UpdateMusicControls();
        _ambientMusicEnabledChanged?.Invoke(AmbientMusicEnabled);
    }

    private void OnMusicVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> eventArgs)
    {
        if (!IsInitialized || _initializing)
        {
            return;
        }

        MusicVolumeValueText.Text = $"{AmbientMusicVolumePercent} %";
        _ambientMusicVolumeChanged?.Invoke(AmbientMusicVolumePercent);
    }

    private void UpdateMusicControls()
    {
        if (!IsInitialized)
        {
            return;
        }

        MusicVolumeSlider.IsEnabled = AmbientMusicEnabled;
        PauseMusicWhenUnfocusedCheck.IsEnabled = AmbientMusicEnabled;
        KeepMusicPlayingWhenHiddenCheck.IsEnabled = AmbientMusicEnabled;
        MusicVolumeValueText.Text = $"{AmbientMusicVolumePercent} %";
    }

    private void OnContinue(object sender, RoutedEventArgs eventArgs)
    {
        EssentialsStep.Visibility = Visibility.Collapsed;
        MusicStep.Visibility = Visibility.Visible;
        StepNumberText.Text = "02";
    }

    private void OnMusicBack(object sender, RoutedEventArgs eventArgs)
    {
        MusicStep.Visibility = Visibility.Collapsed;
        EssentialsStep.Visibility = Visibility.Visible;
        StepNumberText.Text = "01";
    }

    private void OnMusicContinue(object sender, RoutedEventArgs eventArgs)
    {
        MusicStep.Visibility = Visibility.Collapsed;
        PaletteStep.Visibility = Visibility.Visible;
        StepNumberText.Text = "03";
    }

    private void OnBack(object sender, RoutedEventArgs eventArgs)
    {
        PaletteStep.Visibility = Visibility.Collapsed;
        MusicStep.Visibility = Visibility.Visible;
        StepNumberText.Text = "02";
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (IsInitialized)
        {
            AeziolThemeService.Apply(SelectedTheme, _enhanceContrast);
        }
    }

    private void OnFinish(object sender, RoutedEventArgs eventArgs)
    {
        _accepted = true;
        DialogResult = true;
        Close();
    }

    private void OnClose(object sender, RoutedEventArgs eventArgs) => Close();

    protected override void OnClosed(EventArgs e)
    {
        if (!_accepted)
        {
            AeziolThemeService.Apply(_initialTheme, _enhanceContrast);
        }

        base.OnClosed(e);
    }
}
