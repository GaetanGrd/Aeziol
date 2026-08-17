namespace Aeziol.App.Appearance;

internal static class ResponsiveLayout
{
    public const double ReferenceWidth = 900;

    public const double ReferenceHeight = 620;

    public const double MinimumLogicalWidth = 820;

    public const double MinimumLogicalHeight = 560;

    public static double Scale(double availableWidth, double availableHeight)
    {
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            return 1;
        }

        var widthRatio = availableWidth / ReferenceWidth;
        var heightRatio = availableHeight / ReferenceHeight;
        var balancedScale = Math.Sqrt(widthRatio * heightRatio);
        var largestUsableScale = Math.Min(
            availableWidth / MinimumLogicalWidth,
            availableHeight / MinimumLogicalHeight);
        return Math.Min(balancedScale, largestUsableScale);
    }

    public static double LogicalWidth(double availableWidth, double availableHeight) =>
        availableWidth / Scale(availableWidth, availableHeight);

    public static double LogicalHeight(double availableWidth, double availableHeight) =>
        availableHeight / Scale(availableWidth, availableHeight);

    public static (double X, double Y) RightAlignedPopupPosition(
        double popupWidth,
        double targetWidth,
        double targetHeight) =>
        (targetWidth - popupWidth, targetHeight);

    public static double FixedPhysicalLength(
        double physicalLength,
        double availableWidth,
        double availableHeight) =>
        physicalLength / Scale(availableWidth, availableHeight);
}
