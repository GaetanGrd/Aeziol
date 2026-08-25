using System.Windows;
using System.Windows.Controls;

namespace Aeziol.App.DiscordSettingsV4;

public partial class DiscordSettingsV4Concept2 : System.Windows.Controls.UserControl
{
    public DiscordSettingsV4Concept2()
    {
        InitializeComponent();
    }

    private void OnOpenSettingsModal(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string section })
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
                GlobalSettingsPanel.Visibility = Visibility.Visible;
                break;
            case "Outputs":
                SettingsModalTitleText.Text = "Périphériques de sortie";
                ExcludedOutputsSetting.Visibility = Visibility.Visible;
                break;
            case "Fallback":
                SettingsModalTitleText.Text = "Dépannage Discord";
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
