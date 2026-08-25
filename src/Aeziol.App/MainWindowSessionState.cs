using System.Windows;

namespace Aeziol.App;

internal sealed record MainWindowSessionState(
    double Left,
    double Top,
    double Width,
    double Height,
    bool WasMaximized,
    bool SettingsSelected,
    bool DiscordSettingsOpen)
{
    public bool HasValidBounds =>
        double.IsFinite(Left)
        && double.IsFinite(Top)
        && double.IsFinite(Width)
        && double.IsFinite(Height)
        && Width > 0
        && Height > 0;

    public static MainWindowSessionState Capture(Window window, bool settingsSelected, bool discordSettingsOpen)
    {
        ArgumentNullException.ThrowIfNull(window);
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight)
            : window.RestoreBounds;
        return new MainWindowSessionState(
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            window.WindowState == WindowState.Maximized,
            settingsSelected,
            discordSettingsOpen);
    }
}
