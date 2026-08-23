using System.Windows;

namespace Aeziol.App.Appearance;

public enum ButtonTone
{
    Neutral,
    Success,
    Danger,
}

public static class ButtonToneAssist
{
    public static readonly DependencyProperty ToneProperty = DependencyProperty.RegisterAttached(
        "Tone",
        typeof(ButtonTone),
        typeof(ButtonToneAssist),
        new FrameworkPropertyMetadata(ButtonTone.Neutral));

    public static ButtonTone GetTone(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (ButtonTone)element.GetValue(ToneProperty);
    }

    public static void SetTone(DependencyObject element, ButtonTone value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(ToneProperty, value);
    }
}
