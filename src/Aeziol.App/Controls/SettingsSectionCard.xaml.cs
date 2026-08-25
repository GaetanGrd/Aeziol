using System.Windows;
using System.Windows.Media;
using Aeziol.App.Appearance;

namespace Aeziol.App.Controls;

public partial class SettingsSectionCard : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingsSectionCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingsSectionCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionTextProperty = DependencyProperty.Register(
        nameof(ActionText), typeof(string), typeof(SettingsSectionCard), new PropertyMetadata("Configurer"));

    public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(
        nameof(IconData), typeof(Geometry), typeof(SettingsSectionCard), new PropertyMetadata(null));

    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
        nameof(Click), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(SettingsSectionCard));

    public SettingsSectionCard()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public Geometry? IconData
    {
        get => (Geometry?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    private void OnSectionButtonClick(object sender, RoutedEventArgs eventArgs)
    {
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));
        eventArgs.Handled = true;
    }

    private void OnCardMouseEnter(object sender, System.Windows.Input.MouseEventArgs eventArgs)
    {
        if (IsEnabled)
        {
            CardJourneyTrace.ShowHighlight(
                this,
                new Rect(0, 0, CardJourneyTrace.Width, CardJourneyTrace.Height),
                MotionAssist.GetIsReduced(this));
        }
    }

    private void OnCardMouseLeave(object sender, System.Windows.Input.MouseEventArgs eventArgs) =>
        CardJourneyTrace.HideHighlight(this, MotionAssist.GetIsReduced(this));
}
