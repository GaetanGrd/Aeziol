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
    private bool _accepted;
    private bool _syncingLanguageChoices;

    public FirstRunWindow(
        LocalizationService localization,
        string language,
        AeziolTheme theme,
        bool enhanceContrast,
        bool startWithWindows = false,
        bool reduceAnimations = false)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _initialTheme = theme;
        _enhanceContrast = enhanceContrast;
        InitializeComponent();
        SourceInitialized += (_, _) => NativeWindowAppearance.HideSystemBorder(this);
        RefreshLanguageChoices(language);
        FirstRunThemeCombo.SelectedItem = FirstRunThemeCombo.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), theme.ToString(), StringComparison.OrdinalIgnoreCase))
            ?? FirstRunThemeCombo.Items[0];
        AutostartCheck.IsChecked = startWithWindows;
        ReduceAnimationsCheck.IsChecked = reduceAnimations;
        MotionAssist.SetIsReduced(this, reduceAnimations);
        ApplyLocalization();
    }

    public string SelectedLanguage => FirstRunLanguageList.SelectedValue as string ?? "en";
    public bool StartWithWindows => AutostartCheck.IsChecked == true;
    public bool ReduceAnimations => ReduceAnimationsCheck.IsChecked == true;
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

    private void OnContinue(object sender, RoutedEventArgs eventArgs)
    {
        EssentialsStep.Visibility = Visibility.Collapsed;
        PaletteStep.Visibility = Visibility.Visible;
        StepNumberText.Text = "02";
    }

    private void OnBack(object sender, RoutedEventArgs eventArgs)
    {
        PaletteStep.Visibility = Visibility.Collapsed;
        EssentialsStep.Visibility = Visibility.Visible;
        StepNumberText.Text = "01";
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
