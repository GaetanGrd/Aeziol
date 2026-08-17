using Aeziol.App.Appearance;

namespace Aeziol.Tests.App;

public sealed class ResponsiveLayoutTests
{
    [Fact]
    public void ReferenceWindowKeepsItsDesignedLogicalSize()
    {
        Assert.Equal(900, ResponsiveLayout.LogicalWidth(900, 620));
        Assert.Equal(620, ResponsiveLayout.LogicalHeight(900, 620));
        Assert.Equal(1, ResponsiveLayout.Scale(900, 620));
    }

    [Fact]
    public void WideMaximizedWindowGrowsControlsWithoutHorizontalDistortion()
    {
        var logicalWidth = ResponsiveLayout.LogicalWidth(1920, 1080);
        var logicalHeight = ResponsiveLayout.LogicalHeight(1920, 1080);
        var scale = ResponsiveLayout.Scale(1920, 1080);

        Assert.Equal(995.99, logicalWidth, precision: 2);
        Assert.Equal(560.25, logicalHeight, precision: 2);
        Assert.Equal(1.93, scale, precision: 2);
        Assert.Equal(1920, logicalWidth * scale, precision: 2);
        Assert.Equal(1080, logicalHeight * scale, precision: 2);
    }

    [Fact]
    public void IncreasingOnlyTheWidthAlsoIncreasesTheScale()
    {
        var referenceScale = ResponsiveLayout.Scale(900, 620);
        var widerScale = ResponsiveLayout.Scale(1200, 620);

        Assert.True(widerScale > referenceScale);
        Assert.True(ResponsiveLayout.LogicalHeight(1200, 620) >= ResponsiveLayout.MinimumLogicalHeight);
    }

    [Fact]
    public void CloseMenuAlignsItsRightEdgeWithItsPlacementTarget()
    {
        var normal = ResponsiveLayout.RightAlignedPopupPosition(150, 42, 34);
        var scale = ResponsiveLayout.Scale(1920, 1080);
        var scaledPopupWidth = 150 * scale;
        var scaledTargetWidth = 42 * scale;
        var scaledTargetHeight = 34 * scale;
        var scaled = ResponsiveLayout.RightAlignedPopupPosition(
            scaledPopupWidth,
            scaledTargetWidth,
            scaledTargetHeight);

        Assert.Equal((-108, 34), normal);
        Assert.True(scaledPopupWidth > 150);
        Assert.Equal(42, normal.X + 150);
        Assert.Equal(scaledTargetWidth, scaled.X + scaledPopupWidth, precision: 2);
        Assert.Equal(scaledTargetHeight, scaled.Y, precision: 2);
    }

    [Fact]
    public void MaximizedPageGuttersKeepTheirPhysicalSize()
    {
        var scale = ResponsiveLayout.Scale(1920, 1080);
        var logicalRightMargin = ResponsiveLayout.FixedPhysicalLength(30, 1920, 1080);

        Assert.Equal(30, logicalRightMargin * scale, precision: 2);
        Assert.True(logicalRightMargin < 30);
    }
}
