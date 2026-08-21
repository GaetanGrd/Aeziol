using System.Windows;
using System.Windows.Media;
using Aeziol.App.Settings;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace Aeziol.App.Appearance;

public static class AeziolThemeService
{
    public static void Apply(AeziolTheme theme, bool enhanceContrast = false)
    {
        var appearance = GetAppearancePalette(enhanceContrast);
        var palette = GetPalette(theme);
        var uiAccent = SelectUiAccent(palette, appearance, enhanceContrast);
        var isCorrupted = theme == AeziolTheme.Chaos;
        var corruptionSeed = isCorrupted ? Random.Shared.Next() : 0;
        var corruption = isCorrupted ? new Random(corruptionSeed) : null;
        var onAccent = GetContrastingText(uiAccent);
        var onSecondary = GetContrastingText(palette.Secondary);
        var washAlpha = enhanceContrast ? 0x30 : 0x18;
        var lineAlpha = enhanceContrast ? 0xA0 : 0x46;
        var resources = System.Windows.Application.Current?.Resources
            ?? throw new InvalidOperationException("The Aeziol application resources are unavailable.");
        resources["AeziolCorruptedVisuals"] = isCorrupted;
        resources["AeziolCorruptionSeed"] = corruptionSeed;

        Set(resources, "AeziolInkColor", appearance.Ink);
        Set(resources, "AeziolCanvasColor", appearance.Canvas);
        Set(resources, "AeziolRailColor", appearance.Rail);
        Set(resources, "AeziolSurfaceColor", appearance.Surface);
        Set(resources, "AeziolRaisedColor", appearance.Raised);
        Set(resources, "AeziolHoverColor", appearance.Hover);
        Set(resources, "AeziolBorderColor", appearance.Border);
        Set(resources, "AeziolBorderSoftColor", appearance.BorderSoft);
        Set(resources, "AeziolPrimaryColor", palette.Primary);
        Set(resources, "AeziolGoldColor", uiAccent);
        Set(resources, "AeziolGoldBrightColor", uiAccent);
        Set(resources, "AeziolSecondaryColor", palette.Secondary);
        Set(resources, "AeziolOnAccentColor", onAccent);
        Set(resources, "AeziolOnSecondaryColor", onSecondary);
        Set(resources, "AeziolTextColor", appearance.Text);
        Set(resources, "AeziolMutedColor", appearance.Muted);
        Set(resources, "AeziolDimColor", appearance.Dim);
        Set(resources, "AeziolSuccessColor", appearance.Success);
        Set(resources, "AeziolDangerColor", appearance.Danger);
        Set(resources, "AeziolGoldWashColor", WithAlpha(uiAccent, washAlpha));
        Set(resources, "AeziolGoldLineColor", WithAlpha(uiAccent, lineAlpha));
        Set(resources, "AeziolPrimaryTraceColor", WithAlpha(palette.Primary, enhanceContrast ? 0x88 : 0x50));
        Set(resources, "AeziolSecondaryTraceColor", WithAlpha(palette.Secondary, enhanceContrast ? 0x70 : 0x38));
        Set(resources, "AeziolPrimaryTraceBrightColor", WithAlpha(palette.Primary, enhanceContrast ? 0xD0 : 0xA8));
        Set(resources, "AeziolSecondaryTraceBrightColor", WithAlpha(palette.Secondary, enhanceContrast ? 0xB0 : 0x78));
        Set(resources, "AeziolDangerWashColor", WithAlpha(appearance.Danger, 0x20));
        Set(resources, "AeziolRouteMidColor", WithAlpha(appearance.Surface, 0x20));
        Set(resources, "AeziolRouteEdgeColor", WithAlpha(appearance.Surface, 0));

        SetBrush(resources, "AeziolInk", appearance.Ink);
        SetBrush(resources, "AeziolCanvas", appearance.Canvas);
        SetBrush(resources, "AeziolRail", appearance.Rail);
        SetBrush(resources, "AeziolSurface", appearance.Surface);
        SetBrush(resources, "AeziolRaised", appearance.Raised);
        SetBrush(resources, "AeziolHover", appearance.Hover);
        SetBrush(resources, "AeziolBorder", appearance.Border);
        SetBrush(resources, "AeziolBorderSoft", appearance.BorderSoft);
        SetBrush(resources, "AeziolPrimary", palette.Primary);
        SetBrush(resources, "AeziolGold", uiAccent);
        SetBrush(resources, "AeziolGoldBright", uiAccent);
        SetBrush(resources, "AeziolSecondary", palette.Secondary);
        SetBrush(resources, "AeziolOnAccent", onAccent);
        SetBrush(resources, "AeziolOnSecondary", onSecondary);
        SetBrush(resources, "AeziolText", appearance.Text);
        SetBrush(resources, "AeziolMuted", appearance.Muted);
        SetBrush(resources, "AeziolDim", appearance.Dim);
        SetBrush(resources, "AeziolSuccess", appearance.Success);
        SetBrush(resources, "AeziolDanger", appearance.Danger);
        SetBrush(resources, "AeziolGoldWash", WithAlpha(uiAccent, washAlpha));
        SetBrush(resources, "AeziolGoldLine", WithAlpha(uiAccent, lineAlpha));
        SetBrush(resources, "AeziolDangerWash", WithAlpha(appearance.Danger, 0x20));
        SetJourneyDecorationBrushes(resources, palette, appearance, enhanceContrast, corruption);
        if (isCorrupted)
        {
            SetCorruptedInterfaceBrushes(resources, palette, appearance, enhanceContrast, corruption!);
        }

        SetCicadaDrawing(resources, palette, corruption);
    }

    internal static AppearancePalette GetAppearancePalette(bool enhancedContrast) => enhancedContrast
        ? new(
            "#000000", "#000000", "#000000", "#050506", "#0A0A0C", "#121216", "#24242A", "#16161B",
            "#F7F5EF", "#AAA8A1", "#76767E", "#86C7A5", "#E08A82")
        : new(
            "#050506", "#070709", "#09090B", "#0C0C0F", "#121216", "#19191E", "#25252B", "#1B1B20",
            "#F5F3EC", "#AAA8A1", "#74747C", "#86C7A5", "#E08A82");

    internal static ThemePalette GetPalette(AeziolTheme theme) => theme switch
    {
        AeziolTheme.Elgo => new("#F2D98A", "#DEBD68"),
        AeziolTheme.Elna => new("#DEBD68", "#8B6A49"),
        AeziolTheme.Ilyors => new("#AFC7A4", "#DEBD68"),
        AeziolTheme.Cherry => new("#A93D32", "#D56A3E"),
        AeziolTheme.Yuna => new("#9FC3E5", "#FFFFFF"),
        AeziolTheme.Lilith => new("#8068B4", "#A88BE0"),
        AeziolTheme.Chaos => new("#A1A1A1", "#F0F0F0"),
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
    };

    internal static MediaColor SelectUiAccent(ThemePalette palette, AppearancePalette appearance, bool enhanceContrast)
    {
        _ = appearance;
        _ = enhanceContrast;
        return palette.Primary;
    }

    internal static MediaColor GetContrastingText(MediaColor background)
    {
        var blackContrast = ContrastRatio(background, Colors.Black);
        var whiteContrast = ContrastRatio(background, Colors.White);
        return blackContrast >= whiteContrast ? Colors.Black : Colors.White;
    }

    internal static double ContrastRatio(MediaColor first, MediaColor second)
    {
        static double Luminance(MediaColor color)
        {
            static double Linearize(byte channel)
            {
                var value = channel / 255d;
                return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            }

            return (0.2126 * Linearize(color.R)) + (0.7152 * Linearize(color.G)) + (0.0722 * Linearize(color.B));
        }

        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static void Set(ResourceDictionary resources, string key, MediaColor color) => resources[key] = color;

    private static void SetBrush(ResourceDictionary resources, string key, MediaColor color)
    {
        if (resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
            return;
        }

        resources[key] = new SolidColorBrush(color);
    }

    private static void SetJourneyDecorationBrushes(
        ResourceDictionary resources,
        ThemePalette palette,
        AppearancePalette appearance,
        bool enhanceContrast,
        Random? corruption)
    {
        var primaryTrace = WithAlpha(palette.Primary, enhanceContrast ? 0x88 : 0x50);
        var secondaryTrace = WithAlpha(palette.Secondary, enhanceContrast ? 0x70 : 0x38);
        var primaryBright = WithAlpha(palette.Primary, enhanceContrast ? 0xD0 : 0xA8);
        var secondaryBright = WithAlpha(palette.Secondary, enhanceContrast ? 0xB0 : 0x78);

        resources["AeziolTraceBorder"] = corruption is not null
            ? CorruptionBrushFactory.CreateEroded(
                corruption, primaryTrace, secondaryTrace, erosionCount: corruption.Next(2, 5))
            : CreateLinearGradient(
                (Colors.Transparent, 0),
                (primaryTrace, 0.1),
                (appearance.BorderSoft, 0.28),
                (Colors.Transparent, 0.49),
                (secondaryTrace, 0.73),
                (appearance.BorderSoft, 0.9),
                (Colors.Transparent, 1));
        resources["AeziolTraceBorderHover"] = corruption is not null
            ? CorruptionBrushFactory.CreateEroded(
                corruption, primaryBright, secondaryBright, erosionCount: corruption.Next(2, 5))
            : CreateLinearGradient(
                (Colors.Transparent, 0),
                (primaryBright, 0.08),
                (secondaryTrace, 0.31),
                (Colors.Transparent, 0.53),
                (secondaryBright, 0.76),
                (primaryTrace, 0.93),
                (Colors.Transparent, 1));
        resources["AeziolJourneyParticlePrimary"] = CreateParticleGradient(
            palette.Primary,
            palette.Secondary,
            center: new System.Windows.Point(0.35, 0.35),
            radius: 0.72,
            blendOffset: 0.56,
            corruption is not null);
        resources["AeziolJourneyParticleSecondary"] = CreateParticleGradient(
            palette.Secondary,
            palette.Primary,
            center: new System.Windows.Point(0.38, 0.32),
            radius: 0.75,
            blendOffset: 0.6,
            corruption is not null);
    }

    private static LinearGradientBrush CreateLinearGradient(
        params (MediaColor Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 1),
        };
        foreach (var (color, offset) in stops)
        {
            brush.GradientStops.Add(new GradientStop(color, offset));
        }

        brush.Freeze();
        return brush;
    }

    private static RadialGradientBrush CreateParticleGradient(
        MediaColor centerColor,
        MediaColor blendColor,
        System.Windows.Point center,
        double radius,
        double blendOffset,
        bool isCorrupted)
    {
        var brush = new RadialGradientBrush
        {
            Center = center,
            GradientOrigin = center,
            RadiusX = radius,
            RadiusY = radius,
        };
        brush.GradientStops.Add(new GradientStop(centerColor, 0));
        if (isCorrupted)
        {
            brush.GradientStops.Add(new GradientStop(centerColor, 0.3));
            brush.GradientStops.Add(new GradientStop(Colors.Transparent, 0.46));
            brush.GradientStops.Add(new GradientStop(WithAlpha(blendColor, 0x9A), 0.61));
            brush.GradientStops.Add(new GradientStop(WithAlpha(centerColor, 0x38), 0.78));
        }
        else
        {
            brush.GradientStops.Add(new GradientStop(blendColor, blendOffset));
        }

        brush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        brush.Freeze();
        return brush;
    }

    private static void SetCorruptedInterfaceBrushes(
        ResourceDictionary resources,
        ThemePalette palette,
        AppearancePalette appearance,
        bool enhanceContrast,
        Random corruption)
    {
        resources["AeziolCanvas"] = CorruptionBrushFactory.CreateStainedSurface(
            corruption, appearance.Canvas, appearance.Rail, appearance.Surface);
        resources["AeziolRail"] = CorruptionBrushFactory.CreateStainedSurface(
            corruption, appearance.Rail, appearance.Surface, appearance.Ink);
        resources["AeziolSurface"] = CorruptionBrushFactory.CreateStainedSurface(
            corruption, appearance.Surface, appearance.Raised, appearance.Canvas);
        resources["AeziolRaised"] = CorruptionBrushFactory.CreateStainedSurface(
            corruption, appearance.Raised, appearance.Hover, appearance.Surface);
        resources["AeziolHover"] = CorruptionBrushFactory.CreateStainedSurface(
            corruption, appearance.Hover, appearance.Raised, appearance.Surface);

        resources["AeziolBorder"] = CorruptionBrushFactory.CreateEroded(
            corruption,
            appearance.Border,
            WithAlpha(palette.Secondary, 0x54),
            erosionCount: corruption.Next(2, 5));
        resources["AeziolBorderSoft"] = CorruptionBrushFactory.CreateEroded(
            corruption,
            appearance.BorderSoft,
            WithAlpha(palette.Primary, 0x38),
            erosionCount: corruption.Next(2, 5));

        var lineAlpha = enhanceContrast ? 0xA0 : 0x46;
        resources["AeziolGoldWash"] = CorruptionBrushFactory.CreateEroded(
            corruption,
            WithAlpha(palette.Primary, enhanceContrast ? 0x30 : 0x18),
            WithAlpha(palette.Secondary, enhanceContrast ? 0x24 : 0x12),
            erosionCount: corruption.Next(1, 4));
        resources["AeziolGoldLine"] = CorruptionBrushFactory.CreateEroded(
            corruption,
            WithAlpha(palette.Primary, lineAlpha),
            WithAlpha(palette.Secondary, lineAlpha),
            erosionCount: corruption.Next(2, 5));
    }

    private static void SetCicadaDrawing(
        ResourceDictionary resources,
        ThemePalette palette,
        Random? corruption)
    {
        if (resources["AeziolCicadaDrawing"] is not DrawingImage source ||
            source.Drawing is not DrawingGroup)
        {
            return;
        }

        if (resources["AeziolCicadaDrawingTemplate"] is not DrawingImage template)
        {
            template = source.Clone();
            resources["AeziolCicadaDrawingTemplate"] = template;
        }

        var themedDrawing = template.Clone();
        var group = (DrawingGroup)themedDrawing.Drawing;
        var wingBrush = new SolidColorBrush(palette.Secondary);
        var wingWashBrush = new SolidColorBrush(WithAlpha(palette.Secondary, 0x18));
        var bodyBrush = new SolidColorBrush(palette.Primary);
        var ghostFragments = new List<GeometryDrawing>();

        for (var index = 0; index < group.Children.Count; index++)
        {
            if (group.Children[index] is not GeometryDrawing geometry)
            {
                continue;
            }

            if (index is 0 or 1)
            {
                geometry.Brush = wingWashBrush;
            }
            else if (index is 4 or 5)
            {
                geometry.Brush = bodyBrush;
            }

            if (geometry.Pen is not null)
            {
                geometry.Pen.Brush = wingBrush;
            }

            if (corruption is not null)
            {
                if (index is 0 or 1)
                {
                    ghostFragments.Add(CreateCicadaGhost(geometry, palette.Primary, corruption));
                }

                DistortCicadaGeometry(geometry, corruption, index);
            }
        }

        if (corruption is not null)
        {
            foreach (var fragment in ghostFragments)
            {
                group.Children.Add(fragment);
            }

            group.OpacityMask = CorruptionBrushFactory.CreateOpacityMask(
                corruption,
                erosionCount: corruption.Next(3, 7));
        }

        themedDrawing.Freeze();
        resources["AeziolCicadaDrawing"] = themedDrawing;
    }

    private static GeometryDrawing CreateCicadaGhost(
        GeometryDrawing source,
        MediaColor color,
        Random corruption)
    {
        var ghost = source.Clone();
        ghost.Brush = null;
        if (ghost.Pen is not null)
        {
            ghost.Pen.Brush = new SolidColorBrush(WithAlpha(color, corruption.Next(0x24, 0x50)));
            ghost.Pen.Thickness *= corruption.NextDouble() * 0.22 + 0.58;
        }

        var geometry = ghost.Geometry?.Clone();
        if (geometry is not null)
        {
            geometry.Transform = new TranslateTransform(
                SignedDistance(corruption, 5, 12),
                SignedDistance(corruption, 2, 8));
            ghost.Geometry = geometry;
        }

        return ghost;
    }

    private static void DistortCicadaGeometry(
        GeometryDrawing drawing,
        Random corruption,
        int index)
    {
        if (drawing.Geometry is null)
        {
            return;
        }

        var geometry = drawing.Geometry.Clone();
        var transforms = new TransformGroup();
        if (geometry.Transform is { Value.IsIdentity: false } existing)
        {
            transforms.Children.Add(existing.Clone());
        }

        var intensity = index is 0 or 1 ? 1d : 0.58d;
        transforms.Children.Add(new RotateTransform(
            SignedDistance(corruption, 0.18, 1.05) * intensity,
            512,
            index is 4 ? 318 : 512));
        transforms.Children.Add(new TranslateTransform(
            SignedDistance(corruption, 1.2, 6) * intensity,
            SignedDistance(corruption, 0.8, 4.5) * intensity));
        if (index is 4 or 5)
        {
            transforms.Children.Add(new ScaleTransform(
                0.992 + (corruption.NextDouble() * 0.016),
                0.992 + (corruption.NextDouble() * 0.016),
                512,
                500));
        }

        geometry.Transform = transforms;
        drawing.Geometry = geometry;
    }

    private static double SignedDistance(Random corruption, double minimum, double maximum)
    {
        var distance = minimum + (corruption.NextDouble() * (maximum - minimum));
        return corruption.Next(2) == 0 ? -distance : distance;
    }

    private static MediaColor WithAlpha(MediaColor color, int alpha) => MediaColor.FromArgb((byte)alpha, color.R, color.G, color.B);
}

internal sealed record ThemePalette(MediaColor Primary, MediaColor Secondary)
{
    public ThemePalette(string primary, string secondary)
        : this(Parse(primary), Parse(secondary))
    {
    }

    private static MediaColor Parse(string value) => (MediaColor)MediaColorConverter.ConvertFromString(value);
}

internal sealed record AppearancePalette(
    MediaColor Ink,
    MediaColor Canvas,
    MediaColor Rail,
    MediaColor Surface,
    MediaColor Raised,
    MediaColor Hover,
    MediaColor Border,
    MediaColor BorderSoft,
    MediaColor Text,
    MediaColor Muted,
    MediaColor Dim,
    MediaColor Success,
    MediaColor Danger)
{
    public AppearancePalette(
        string ink,
        string canvas,
        string rail,
        string surface,
        string raised,
        string hover,
        string border,
        string borderSoft,
        string text,
        string muted,
        string dim,
        string success,
        string danger)
        : this(
            Parse(ink), Parse(canvas), Parse(rail), Parse(surface), Parse(raised), Parse(hover), Parse(border), Parse(borderSoft),
            Parse(text), Parse(muted), Parse(dim), Parse(success), Parse(danger))
    {
    }

    private static MediaColor Parse(string value) => (MediaColor)MediaColorConverter.ConvertFromString(value);
}
