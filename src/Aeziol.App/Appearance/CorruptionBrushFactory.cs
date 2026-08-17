using System.Windows;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace Aeziol.App.Appearance;

internal static class CorruptionBrushFactory
{
    private static readonly Rect BrushBounds = new(0, 0, 100, 100);

    public static DrawingBrush CreateStainedSurface(
        Random corruption,
        MediaColor foundation,
        MediaColor firstVariation,
        MediaColor secondVariation)
    {
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(
            new SolidColorBrush(foundation),
            null,
            new RectangleGeometry(BrushBounds)));

        var stainCount = corruption.Next(4, 9);
        for (var index = 0; index < stainCount; index++)
        {
            var color = corruption.Next(2) == 0 ? firstVariation : secondVariation;
            var alpha = (byte)corruption.Next(0x16, 0x48);
            var center = new WpfPoint(corruption.NextDouble() * 86 + 7, corruption.NextDouble() * 86 + 7);
            drawing.Children.Add(new GeometryDrawing(
                new SolidColorBrush(MediaColor.FromArgb(alpha, color.R, color.G, color.B)),
                null,
                CreateOrganicMass(
                    corruption,
                    center,
                    corruption.NextDouble() * 14 + 8,
                    corruption.NextDouble() * 18 + 9)));
        }

        return CreateBrush(drawing);
    }

    public static DrawingBrush CreateEroded(
        Random corruption,
        MediaColor foundation,
        MediaColor intrusion,
        int erosionCount)
    {
        var visibleMatter = new GeometryGroup { FillRule = FillRule.EvenOdd };
        visibleMatter.Children.Add(new RectangleGeometry(BrushBounds));
        for (var index = 0; index < erosionCount; index++)
        {
            visibleMatter.Children.Add(CreateOrganicMass(
                corruption,
                RandomEdgePoint(corruption),
                corruption.NextDouble() * 8 + 4,
                corruption.NextDouble() * 9 + 4));
        }

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(foundation), null, visibleMatter));
        var intrusionCount = corruption.Next(2, 5);
        for (var index = 0; index < intrusionCount; index++)
        {
            var alpha = (byte)Math.Min(intrusion.A, corruption.Next(0x20, 0x78));
            drawing.Children.Add(new GeometryDrawing(
                new SolidColorBrush(MediaColor.FromArgb(alpha, intrusion.R, intrusion.G, intrusion.B)),
                null,
                CreateOrganicMass(
                    corruption,
                    RandomEdgePoint(corruption),
                    corruption.NextDouble() * 7 + 3,
                    corruption.NextDouble() * 8 + 3)));
        }

        return CreateBrush(drawing);
    }

    public static DrawingBrush CreateOpacityMask(
        Random corruption,
        int erosionCount,
        MediaBrush? matterBrush = null)
    {
        var visibleMatter = new GeometryGroup { FillRule = FillRule.EvenOdd };
        visibleMatter.Children.Add(new RectangleGeometry(BrushBounds));
        for (var index = 0; index < erosionCount; index++)
        {
            visibleMatter.Children.Add(CreateOrganicMass(
                corruption,
                new WpfPoint(corruption.NextDouble() * 82 + 9, corruption.NextDouble() * 82 + 9),
                corruption.NextDouble() * 8 + 4,
                corruption.NextDouble() * 10 + 5));
        }

        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(
            matterBrush ?? System.Windows.Media.Brushes.White,
            null,
            visibleMatter));
        return CreateBrush(drawing);
    }

    private static WpfPoint RandomEdgePoint(Random corruption)
    {
        var along = corruption.NextDouble() * 88 + 6;
        var inset = corruption.NextDouble() * 9 - 3;
        return corruption.Next(4) switch
        {
            0 => new WpfPoint(along, inset),
            1 => new WpfPoint(100 - inset, along),
            2 => new WpfPoint(along, 100 - inset),
            _ => new WpfPoint(inset, along),
        };
    }

    private static GeometryGroup CreateOrganicMass(
        Random corruption,
        WpfPoint center,
        double radiusX,
        double radiusY)
    {
        NormalizeAspectRatio(ref radiusX, ref radiusY);

        var mass = new GeometryGroup { FillRule = FillRule.Nonzero };
        mass.Children.Add(CreateLobedPath(corruption, center, radiusX, radiusY, 0.42));

        var lobeCount = corruption.Next(2, 6);
        for (var index = 0; index < lobeCount; index++)
        {
            var angle = corruption.NextDouble() * Math.PI * 2;
            var distance = corruption.NextDouble() * 0.42 + 0.36;
            var lobeCenter = new WpfPoint(
                center.X + (Math.Cos(angle) * radiusX * distance),
                center.Y + (Math.Sin(angle) * radiusY * distance));
            var lobeScale = corruption.NextDouble() * 0.28 + 0.26;
            mass.Children.Add(CreateLobedPath(
                corruption,
                lobeCenter,
                radiusX * lobeScale * (corruption.NextDouble() * 0.45 + 0.78),
                radiusY * lobeScale * (corruption.NextDouble() * 0.45 + 0.78),
                0.34));
        }

        mass.Transform = new RotateTransform(corruption.NextDouble() * 54 - 27, center.X, center.Y);
        mass.Freeze();
        return mass;
    }

    private static StreamGeometry CreateLobedPath(
        Random corruption,
        WpfPoint center,
        double radiusX,
        double radiusY,
        double irregularity)
    {
        var pointCount = corruption.Next(9, 15);
        var points = new WpfPoint[pointCount];
        var primaryPhase = corruption.NextDouble() * Math.PI * 2;
        var secondaryPhase = corruption.NextDouble() * Math.PI * 2;
        var centerPullX = (corruption.NextDouble() - 0.5) * radiusX * 0.3;
        var centerPullY = (corruption.NextDouble() - 0.5) * radiusY * 0.3;
        for (var index = 0; index < pointCount; index++)
        {
            var angle = ((Math.PI * 2 * index) / pointCount)
                + ((corruption.NextDouble() - 0.5) * 0.18);
            var harmonic = (Math.Sin((angle * 2) + primaryPhase) * irregularity * 0.46)
                + (Math.Sin((angle * 3) + secondaryPhase) * irregularity * 0.32);
            var radialJitter = Math.Clamp(
                1 + harmonic + ((corruption.NextDouble() - 0.5) * irregularity),
                0.56,
                1.42);
            points[index] = new WpfPoint(
                center.X + centerPullX + (Math.Cos(angle) * radiusX * radialJitter),
                center.Y + centerPullY + (Math.Sin(angle) * radiusY * radialJitter));
        }

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(points[0], isFilled: true, isClosed: true);
            for (var index = 0; index < pointCount; index++)
            {
                var previous = points[(index - 1 + pointCount) % pointCount];
                var current = points[index];
                var next = points[(index + 1) % pointCount];
                var afterNext = points[(index + 2) % pointCount];
                var controlA = new WpfPoint(
                    current.X + ((next.X - previous.X) / 6),
                    current.Y + ((next.Y - previous.Y) / 6));
                var controlB = new WpfPoint(
                    next.X - ((afterNext.X - current.X) / 6),
                    next.Y - ((afterNext.Y - current.Y) / 6));
                context.BezierTo(controlA, controlB, next, isStroked: true, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        return geometry;
    }

    private static void NormalizeAspectRatio(ref double radiusX, ref double radiusY)
    {
        const double maximumRatio = 1.65;
        if (radiusX > radiusY * maximumRatio)
        {
            radiusX = radiusY * maximumRatio;
        }
        else if (radiusY > radiusX * maximumRatio)
        {
            radiusY = radiusX * maximumRatio;
        }
    }

    private static DrawingBrush CreateBrush(Drawing drawing)
    {
        var brush = new DrawingBrush(drawing)
        {
            Viewbox = BrushBounds,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 1, 1),
            ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            Stretch = Stretch.Fill,
        };
        brush.Freeze();
        return brush;
    }
}
