using System.Windows;
using System.Windows.Controls;

namespace Aeziol.App.DiscordSettingsV4;

public partial class DiscordSettingsV4Concept2 : System.Windows.Controls.UserControl
{
    private const int MaximumRestoreDelaySeconds = 30;
    private bool _syncingRestoreDelay;

    public event Action<int>? RestoreDelayChanged;

    public int RestoreDelaySeconds { get; private set; } = 1;

    public DiscordSettingsV4Concept2()
    {
        InitializeComponent();
    }

    public void SetRestoreDelaySeconds(int seconds)
    {
        var normalizedSeconds = Math.Clamp(seconds, 0, MaximumRestoreDelaySeconds);
        _syncingRestoreDelay = true;
        try
        {
            RestoreDelaySeconds = normalizedSeconds;
            if (normalizedSeconds <= 3)
            {
                RestoreDelayComboBox.SelectedIndex = normalizedSeconds;
                CustomRestoreDelayPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                RestoreDelayComboBox.SelectedIndex = 4;
                CustomRestoreDelayTextBox.Text = normalizedSeconds.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
                CustomRestoreDelayPanel.Visibility = Visibility.Visible;
            }
        }
        finally
        {
            _syncingRestoreDelay = false;
        }
    }

    private void OnRestoreDelaySelectionChanged(object sender, SelectionChangedEventArgs eventArgs)
    {
        if (CustomRestoreDelayPanel is null
            || RestoreDelayComboBox.SelectedItem is not ComboBoxItem selectedItem)
        {
            return;
        }

        var tag = selectedItem.Tag?.ToString();
        var isCustom = string.Equals(tag, "Custom", StringComparison.Ordinal);
        CustomRestoreDelayPanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
        if (_syncingRestoreDelay)
        {
            return;
        }

        if (isCustom)
        {
            PublishCustomRestoreDelay();
        }
        else if (int.TryParse(tag, out var seconds))
        {
            PublishRestoreDelay(seconds);
        }
    }

    private void OnCustomRestoreDelayTextChanged(object sender, TextChangedEventArgs eventArgs)
    {
        if (!_syncingRestoreDelay
            && RestoreDelayComboBox.SelectedIndex == 4)
        {
            PublishCustomRestoreDelay();
        }
    }

    private void OnCustomRestoreDelayPreviewTextInput(
        object sender,
        System.Windows.Input.TextCompositionEventArgs eventArgs) =>
        eventArgs.Handled = eventArgs.Text.Any(character => !char.IsDigit(character));

    private void PublishCustomRestoreDelay()
    {
        if (int.TryParse(CustomRestoreDelayTextBox.Text, out var seconds)
            && seconds is >= 0 and <= MaximumRestoreDelaySeconds)
        {
            PublishRestoreDelay(seconds);
        }
    }

    private void PublishRestoreDelay(int seconds)
    {
        if (RestoreDelaySeconds == seconds)
        {
            return;
        }

        RestoreDelaySeconds = seconds;
        RestoreDelayChanged?.Invoke(seconds);
    }

    private void OnOpenSettingsModal(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Aeziol.App.Controls.SettingsSectionCard { Tag: string section })
        {
            return;
        }

        GlobalSettingsPanel.Visibility = Visibility.Collapsed;
        ExcludedOutputsSetting.Visibility = Visibility.Collapsed;
        FallbackSettingsPanel.Visibility = Visibility.Collapsed;

        switch (section)
        {
            case "Global":
                SettingsModalTitleText.Text = "Réglages Discord globaux";
                SettingsModalHeaderIcon.Data = System.Windows.Media.Geometry.Parse(
                    "M 3,8 L 3,13 L 7,13 L 12,17 L 12,3 L 7,7 L 3,7 Z M 15,7 C 17,9 17,11 15,13");
                GlobalSettingsPanel.Visibility = Visibility.Visible;
                break;
            case "Outputs":
                SettingsModalTitleText.Text = "Périphériques de sortie";
                SettingsModalHeaderIcon.Data = System.Windows.Media.Geometry.Parse(
                    "M 3,5 L 3,15 L 17,15 L 17,5 Z M 6,5 L 7,2 L 13,2 L 14,5 M 6,9 L 14,9");
                ExcludedOutputsSetting.Visibility = Visibility.Visible;
                break;
            case "Fallback":
                SettingsModalTitleText.Text = "Dépannage Discord";
                SettingsModalHeaderIcon.Data = System.Windows.Media.Geometry.Parse(
                    "M 3,4 L 17,4 L 17,16 L 3,16 Z M 6,8 L 14,8 M 6,12 L 11,12");
                FallbackSettingsPanel.Visibility = Visibility.Visible;
                break;
            default:
                return;
        }

        SettingsModalScrollViewer.ScrollToTop();
        SettingsModalLayer.Visibility = Visibility.Visible;
        SettingsModalCloseButton.Focus();
        eventArgs.Handled = true;
    }

    private void OnCloseSettingsModal(object sender, RoutedEventArgs eventArgs)
    {
        HideSettingsModal();
        eventArgs.Handled = true;
    }

    private void OnSettingsModalBackdropClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.OriginalSource, SettingsModalLayer))
        {
            HideSettingsModal();
            eventArgs.Handled = true;
        }
    }

    private void OnSettingsModalSurfaceClick(
        object sender,
        System.Windows.Input.MouseButtonEventArgs eventArgs) =>
        eventArgs.Handled = true;

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == System.Windows.Input.Key.Escape && SettingsModalLayer.Visibility == Visibility.Visible)
        {
            HideSettingsModal();
            eventArgs.Handled = true;
        }
    }

    private void HideSettingsModal()
    {
        SettingsModalLayer.Visibility = Visibility.Collapsed;
        GlobalSettingsPanel.Visibility = Visibility.Collapsed;
        ExcludedOutputsSetting.Visibility = Visibility.Collapsed;
        FallbackSettingsPanel.Visibility = Visibility.Collapsed;
    }
}
