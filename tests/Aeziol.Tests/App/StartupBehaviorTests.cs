using System.Runtime.InteropServices;
using Aeziol.App.Services;

namespace Aeziol.Tests.App;

public sealed class StartupBehaviorTests
{
#pragma warning disable CA2201 // These tests intentionally reproduce COM exceptions raised by WinRT.
    [Fact]
    public void IsWindowsStartup_WhenUnpackagedActivationIsUnavailable_ReturnsFalse()
    {
        var exception = new COMException(
            "Activation metadata is unavailable outside a package.",
            unchecked((int)0xD0000225));

        var result = Aeziol.App.App.IsWindowsStartup([], () => throw exception);

        Assert.False(result);
    }

    [Fact]
    public void IsWindowsStartup_WhenActivationFailsUnexpectedly_PropagatesTheException()
    {
        var exception = new COMException("Unexpected activation failure.", unchecked((int)0x80004005));

        var actual = Assert.Throws<COMException>(
            () => Aeziol.App.App.IsWindowsStartup([], () => throw exception));

        Assert.Same(exception, actual);
    }
#pragma warning restore CA2201

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, true, true)]
    public void ShouldShowMainWindow_OnlyHidesAnAutomaticStartupWhenRequested(
        bool activationRequested,
        bool isWindowsStartup,
        bool openHiddenAtWindowsStartup,
        bool expected)
    {
        Assert.Equal(
            expected,
            Aeziol.App.App.ShouldShowMainWindow(
                activationRequested,
                isWindowsStartup,
                openHiddenAtWindowsStartup));
    }

    [Theory]
    [InlineData(false, "\"C:\\Apps\\Aeziol.exe\"")]
    [InlineData(true, "\"C:\\Apps\\Aeziol.exe\" --background")]
    public void BuildRunCommand_AddsTheBackgroundArgumentOnlyWhenRequested(
        bool openHidden,
        string expected)
    {
        Assert.Equal(expected, AutostartService.BuildRunCommand(@"C:\Apps\Aeziol.exe", openHidden));
    }
}
