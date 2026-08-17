using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Aeziol.App.Appearance;

internal static partial class NativeWindowAppearance
{
    private const int DwmWindowAttributeCornerPreference = 33;
    private const int DwmWindowAttributeBorderColor = 34;
    private const int DwmWindowCornerPreferenceRound = 2;
    private const uint DwmColorNone = 0xFFFFFFFE;

    public static void HideSystemBorder(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0)
        {
            return;
        }

        var color = DwmColorNone;
        var cornerPreference = DwmWindowCornerPreferenceRound;
        _ = DwmSetWindowAttribute(handle, DwmWindowAttributeCornerPreference, ref cornerPreference, sizeof(int));
        _ = DwmSetWindowAttribute(handle, DwmWindowAttributeBorderColor, ref color, sizeof(uint));
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint windowHandle, int attribute, ref uint value, int valueSize);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint windowHandle, int attribute, ref int value, int valueSize);
}
