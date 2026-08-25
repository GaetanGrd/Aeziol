using System.Windows;
using System.Windows.Controls.Primitives;

namespace Aeziol.App.DiscordSettingsV4;

public partial class DiscordSettingsV4Gallery : System.Windows.Controls.UserControl
{
    private readonly FrameworkElement[] _concepts;

    public DiscordSettingsV4Gallery()
    {
        InitializeComponent();
        _concepts = [Concept1, Concept2, Concept3, Concept4];
        Concept1Button.IsChecked = true;
        ShowConcept(1);
    }

    private void OnConceptSelected(object sender, RoutedEventArgs eventArgs)
    {
        if (sender is ToggleButton { Tag: string selection }
            && int.TryParse(selection, out var conceptNumber))
        {
            ShowConcept(conceptNumber);
        }
    }

    private void ShowConcept(int conceptNumber)
    {
        for (var index = 0; index < _concepts.Length; index++)
        {
            _concepts[index].Visibility = index == conceptNumber - 1
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}
