using Aeziol.App.Services;

namespace Aeziol.Tests.App;

public sealed class StartupBehaviorTests
{
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
