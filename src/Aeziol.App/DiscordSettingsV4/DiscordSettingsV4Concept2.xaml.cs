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

}
