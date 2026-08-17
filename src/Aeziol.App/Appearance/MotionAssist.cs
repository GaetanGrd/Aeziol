using System.Windows;

namespace Aeziol.App.Appearance;

public static class MotionAssist
{
    public static readonly DependencyProperty IsReducedProperty = DependencyProperty.RegisterAttached(
        "IsReduced",
        typeof(bool),
        typeof(MotionAssist),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    public static bool GetIsReduced(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (bool)element.GetValue(IsReducedProperty);
    }

    public static void SetIsReduced(DependencyObject element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(IsReducedProperty, value);
    }
}
