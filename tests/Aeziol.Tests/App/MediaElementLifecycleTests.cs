using System.Windows.Controls;

namespace Aeziol.Tests.App;

[Collection(WpfUiTestGroup.Name)]
public sealed class MediaElementLifecycleTests
{
    [Fact]
    public void ReleaseMediaElementClearsTheSource()
    {
        WpfTestHost.Run(() =>
        {
            var host = new Grid();
            var mediaElement = new MediaElement
            {
                LoadedBehavior = MediaState.Manual,
                Source = new Uri("file:///C:/nonexistent-aeziol-media.mp4"),
            };
            var fallbackElement = new System.Windows.Controls.Image();
            host.Children.Add(mediaElement);
            host.Children.Add(fallbackElement);

            var releasedMedia = Aeziol.App.MainWindow.ReleaseMediaElement(mediaElement);

            Assert.True(releasedMedia);
            Assert.Null(mediaElement.Source);
            Assert.Equal(MediaState.Close, mediaElement.UnloadedBehavior);
            Assert.Null(mediaElement.Parent);

            Aeziol.App.MainWindow.AttachMediaElement(mediaElement, fallbackElement);

            Assert.Same(host, mediaElement.Parent);
            Assert.Equal(0, host.Children.IndexOf(mediaElement));
            Assert.False(Aeziol.App.MainWindow.ReleaseMediaElement(mediaElement));
        });
    }
}
