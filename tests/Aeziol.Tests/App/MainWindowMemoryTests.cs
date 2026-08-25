using Aeziol.App;

namespace Aeziol.Tests.App;

public sealed class MainWindowMemoryTests
{
    [Fact]
    public void LoadDecodedBitmap_DecodesAtRequestedWidthAndFreezesImage()
    {
        WpfTestHost.Run(() =>
        {
            var bitmap = MainWindow.LoadDecodedBitmap(
                "Assets/Audio/onde-doree-cover.png",
                decodePixelWidth: 320);

            Assert.Equal(320, bitmap.PixelWidth);
            Assert.True(bitmap.IsFrozen);
        });
    }

    [Fact]
    public void SessionState_RequiresFinitePositiveBounds()
    {
        var valid = new MainWindowSessionState(20, 30, 1125, 775, false, true, false);
        var invalid = valid with { Width = double.NaN };

        Assert.True(valid.HasValidBounds);
        Assert.False(invalid.HasValidBounds);
    }
}
