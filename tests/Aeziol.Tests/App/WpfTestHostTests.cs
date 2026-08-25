using System.Windows;

namespace Aeziol.Tests.App;

[Collection(WpfUiTestGroup.Name)]
public sealed class WpfTestHostTests
{
    [Fact]
    public void HostLoadsAeziolResourcesWithoutStartingTheProductWindow()
    {
        WpfTestHost.Run(() =>
        {
            var application = Assert.IsType<Aeziol.App.App>(System.Windows.Application.Current);

            Assert.Null(application.MainWindow);
            Assert.Empty(application.Windows.Cast<Window>());
            Assert.NotNull(application.TryFindResource("AeziolSurface"));
        });
    }
}
