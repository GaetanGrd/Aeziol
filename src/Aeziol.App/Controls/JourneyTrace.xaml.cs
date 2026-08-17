using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Aeziol.App.Appearance;
using WpfPath = System.Windows.Shapes.Path;

namespace Aeziol.App.Controls;

public enum JourneyTraceOrientation
{
    Horizontal,
    Vertical,
}

public partial class JourneyTrace : System.Windows.Controls.UserControl
{
    private static int _nextCorruptionIdentity;
    private static readonly TimeSpan DefaultHighlightInDuration = TimeSpan.FromMilliseconds(280);
    private static readonly TimeSpan DefaultHighlightOutDuration = TimeSpan.FromMilliseconds(650);

    private readonly object _anonymousOwner = new();
    private readonly int _corruptionIdentity = System.Threading.Interlocked.Increment(ref _nextCorruptionIdentity);
    private readonly Dictionary<object, HighlightState> _highlights = new(ReferenceEqualityComparer.Instance);
    private object? _highlightOwner;
    private int _nextZIndex;

    public static readonly DependencyProperty TraceAProperty = DependencyProperty.Register(
        nameof(TraceA), typeof(Geometry), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnTraceChanged));

    public static readonly DependencyProperty TraceBProperty = DependencyProperty.Register(
        nameof(TraceB), typeof(Geometry), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnTraceChanged));

    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(JourneyTraceOrientation), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(JourneyTraceOrientation.Vertical, OnAppearanceChanged));

    public static readonly DependencyProperty EdgeFadeProperty = DependencyProperty.Register(
        nameof(EdgeFade), typeof(double), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(0.08d, OnAppearanceChanged));

    public static readonly DependencyProperty HighlightFadeProperty = DependencyProperty.Register(
        nameof(HighlightFade), typeof(double), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(0.2d, OnAppearanceChanged));

    public static readonly DependencyProperty HighlightRadiusProperty = DependencyProperty.Register(
        nameof(HighlightRadius), typeof(double), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(8d, OnAppearanceChanged));

    public static readonly DependencyProperty BaseStrokeAProperty = DependencyProperty.Register(
        nameof(BaseStrokeA), typeof(double), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(0.9d, OnAppearanceChanged));

    public static readonly DependencyProperty BaseStrokeBProperty = DependencyProperty.Register(
        nameof(BaseStrokeB), typeof(double), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(0.75d, OnAppearanceChanged));

    public static readonly DependencyProperty BaseOpacityAProperty = DependencyProperty.Register(
        nameof(BaseOpacityA), typeof(double), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(0.72d, OnAppearanceChanged));

    public static readonly DependencyProperty BaseOpacityBProperty = DependencyProperty.Register(
        nameof(BaseOpacityB), typeof(double), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(0.42d, OnAppearanceChanged));

    public static readonly DependencyProperty HighlightStrokeProperty = DependencyProperty.Register(
        nameof(HighlightStroke), typeof(double), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(1.7d, OnAppearanceChanged));

    public static readonly DependencyProperty GlowStrokeProperty = DependencyProperty.Register(
        nameof(GlowStroke), typeof(double), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(5d, OnAppearanceChanged));

    public static readonly DependencyProperty DisplayScaleProperty = DependencyProperty.Register(
        nameof(DisplayScale), typeof(double), typeof(JourneyTrace),
        new FrameworkPropertyMetadata(
            1d,
            OnAppearanceChanged,
            static (_, baseValue) => CoerceDisplayScale((double)baseValue)));

    public JourneyTrace()
    {
        InitializeComponent();
        Particles.CollectionChanged += OnParticlesChanged;
        Loaded += (_, _) => RefreshVisuals();
    }

    public Geometry? TraceA
    {
        get => (Geometry?)GetValue(TraceAProperty);
        set => SetValue(TraceAProperty, value);
    }

    public Geometry? TraceB
    {
        get => (Geometry?)GetValue(TraceBProperty);
        set => SetValue(TraceBProperty, value);
    }

    public JourneyTraceOrientation Orientation
    {
        get => (JourneyTraceOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double EdgeFade
    {
        get => (double)GetValue(EdgeFadeProperty);
        set => SetValue(EdgeFadeProperty, value);
    }

    public double HighlightFade
    {
        get => (double)GetValue(HighlightFadeProperty);
        set => SetValue(HighlightFadeProperty, value);
    }

    public double HighlightRadius
    {
        get => (double)GetValue(HighlightRadiusProperty);
        set => SetValue(HighlightRadiusProperty, value);
    }

    public double BaseStrokeA
    {
        get => (double)GetValue(BaseStrokeAProperty);
        set => SetValue(BaseStrokeAProperty, value);
    }

    public double BaseStrokeB
    {
        get => (double)GetValue(BaseStrokeBProperty);
        set => SetValue(BaseStrokeBProperty, value);
    }

    public double BaseOpacityA
    {
        get => (double)GetValue(BaseOpacityAProperty);
        set => SetValue(BaseOpacityAProperty, value);
    }

    public double BaseOpacityB
    {
        get => (double)GetValue(BaseOpacityBProperty);
        set => SetValue(BaseOpacityBProperty, value);
    }

    public double HighlightStroke
    {
        get => (double)GetValue(HighlightStrokeProperty);
        set => SetValue(HighlightStrokeProperty, value);
    }

    public double GlowStroke
    {
        get => (double)GetValue(GlowStrokeProperty);
        set => SetValue(GlowStrokeProperty, value);
    }

    public double DisplayScale
    {
        get => (double)GetValue(DisplayScaleProperty);
        set => SetValue(DisplayScaleProperty, value);
    }

    public ObservableCollection<JourneyParticle> Particles { get; } = [];

    internal Rect CurrentHighlightRect =>
        _highlightOwner is not null && _highlights.TryGetValue(_highlightOwner, out var state)
            ? state.Region
            : Rect.Empty;

    internal int RenderedBaseParticleCount => BaseParticleLayer.Children.Count;

    internal double RenderedBaseParticleWidth =>
        BaseParticleLayer.Children.OfType<Ellipse>().FirstOrDefault()?.Width ?? 0;

    internal bool UsesSpottedCorruptionMask => TraceRoot.OpacityMask is DrawingBrush;

    internal int HighlightLayerCount => _highlights.Count;

    internal int OutgoingHighlightCount =>
        _highlights.Count - (_highlightOwner is not null && _highlights.ContainsKey(_highlightOwner) ? 1 : 0);

    internal double HighlightOpacity =>
        _highlightOwner is not null && _highlights.TryGetValue(_highlightOwner, out var state)
            ? state.Layer.Opacity
            : 0;

    public void ShowHighlight(Rect requestedRegion, bool reduceMotion)
        => ShowHighlight(_anonymousOwner, requestedRegion, reduceMotion);

    public void ShowHighlight(object owner, Rect requestedRegion, bool reduceMotion)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var region = NormalizeRegion(requestedRegion);
        if (region.IsEmpty)
        {
            HideHighlight(owner, reduceMotion);
            return;
        }

        if (_highlightOwner is not null
            && !ReferenceEquals(_highlightOwner, owner)
            && _highlights.TryGetValue(_highlightOwner, out var previous))
        {
            AnimateHighlight(previous, visible: false, reduceMotion);
        }

        _highlightOwner = owner;
        var state = GetOrCreateHighlight(owner);
        ConfigureHighlight(state, region);
        System.Windows.Controls.Panel.SetZIndex(state.Layer, ++_nextZIndex);
        AnimateHighlight(state, visible: true, reduceMotion);
    }

    public void HideHighlight(object owner, bool reduceMotion)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (!ReferenceEquals(_highlightOwner, owner))
        {
            return;
        }

        _highlightOwner = null;
        if (_highlights.TryGetValue(owner, out var state))
        {
            AnimateHighlight(state, visible: false, reduceMotion);
        }
    }

    public void HideHighlight(bool reduceMotion)
    {
        if (_highlightOwner is null)
        {
            return;
        }

        var owner = _highlightOwner;
        _highlightOwner = null;
        if (_highlights.TryGetValue(owner, out var state))
        {
            AnimateHighlight(state, visible: false, reduceMotion);
        }
    }

    private static void OnTraceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        => ((JourneyTrace)dependencyObject).RefreshTraceGeometry();

    private static void OnAppearanceChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        => ((JourneyTrace)dependencyObject).RefreshVisuals();

    private static double CoerceDisplayScale(double scale)
    {
        return double.IsFinite(scale) && scale > 0 ? scale : 1d;
    }

    private void OnParticlesChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs) => RebuildParticles();

    public void RefreshPalette() => RefreshVisuals();

    private HighlightState GetOrCreateHighlight(object owner)
    {
        if (_highlights.TryGetValue(owner, out var existing))
        {
            return existing;
        }

        var clip = new RectangleGeometry();
        var mask = new LinearGradientBrush { MappingMode = BrushMappingMode.Absolute };
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
        mask.GradientStops.Add(new GradientStop(Colors.White, 0.2));
        mask.GradientStops.Add(new GradientStop(Colors.White, 0.8));
        mask.GradientStops.Add(new GradientStop(Colors.Transparent, 1));

        var glowA = CreateHighlightPath(GlowStroke, 0.2);
        var glowB = CreateHighlightPath(GlowStroke, 0.18);
        var traceA = CreateHighlightPath(HighlightStroke, 1);
        var traceB = CreateHighlightPath(HighlightStroke, 1);
        var particles = new Canvas();
        var layer = new Canvas
        {
            Opacity = 0,
            Clip = clip,
            OpacityMask = mask,
            IsHitTestVisible = false,
        };
        layer.Children.Add(glowA);
        layer.Children.Add(glowB);
        layer.Children.Add(traceA);
        layer.Children.Add(traceB);
        layer.Children.Add(particles);

        var state = new HighlightState(owner, layer, clip, mask, glowA, glowB, traceA, traceB, particles);
        _highlights.Add(owner, state);
        HighlightHost.Children.Add(layer);
        RefreshHighlightVisuals(state);
        return state;
    }

    private static WpfPath CreateHighlightPath(double strokeThickness, double opacity) => new()
    {
        StrokeThickness = strokeThickness,
        Opacity = opacity,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };

    private void ConfigureHighlight(HighlightState state, Rect region)
    {
        state.Region = region;
        state.Clip.Rect = region;
        state.Clip.RadiusX = HighlightRadius;
        state.Clip.RadiusY = HighlightRadius;

        var fade = Math.Clamp(HighlightFade, 0, 0.49);
        state.Mask.StartPoint = Orientation == JourneyTraceOrientation.Horizontal
            ? new System.Windows.Point(region.Left, 0)
            : new System.Windows.Point(0, region.Top);
        state.Mask.EndPoint = Orientation == JourneyTraceOrientation.Horizontal
            ? new System.Windows.Point(region.Right, 0)
            : new System.Windows.Point(0, region.Bottom);
        state.Mask.GradientStops[1].Offset = fade;
        state.Mask.GradientStops[2].Offset = 1 - fade;

        var primary = FindColor("AeziolPrimaryColor", Colors.Gold);
        var secondary = FindColor("AeziolSecondaryColor", Colors.Goldenrod);
        var firstGradient = CreateGradient(
            new System.Windows.Point(region.Left, region.Top),
            new System.Windows.Point(region.Right, region.Bottom),
            primary,
            secondary);
        var secondGradient = CreateGradient(
            new System.Windows.Point(region.Right, region.Top),
            new System.Windows.Point(region.Left, region.Bottom),
            secondary,
            primary);
        state.GlowA.Stroke = firstGradient;
        state.TraceA.Stroke = firstGradient;
        state.GlowB.Stroke = secondGradient;
        state.TraceB.Stroke = secondGradient;
    }

    private void RefreshVisuals()
    {
        if (!IsInitialized)
        {
            return;
        }

        RefreshTraceGeometry();
        BaseTraceA.StrokeThickness = BaseStrokeA;
        BaseTraceA.Opacity = BaseOpacityA;
        BaseTraceB.StrokeThickness = BaseStrokeB;
        BaseTraceB.Opacity = BaseOpacityB;
        ConfigureEdgeMask();
        RebuildParticles();
        foreach (var state in _highlights.Values)
        {
            RefreshHighlightVisuals(state);
            ConfigureHighlight(state, state.Region);
        }
    }

    private void RefreshHighlightVisuals(HighlightState state)
    {
        state.GlowA.Data = TraceA;
        state.GlowA.StrokeThickness = GlowStroke;
        state.GlowA.Opacity = 0.2;
        state.GlowB.Data = TraceB;
        state.GlowB.StrokeThickness = GlowStroke;
        state.GlowB.Opacity = 0.18;
        state.TraceA.Data = TraceA;
        state.TraceA.StrokeThickness = HighlightStroke;
        state.TraceB.Data = TraceB;
        state.TraceB.StrokeThickness = HighlightStroke;
        RebuildHighlightParticles(state);
    }

    private void RefreshTraceGeometry()
    {
        if (!IsInitialized)
        {
            return;
        }

        BaseTraceA.Data = TraceA;
        BaseTraceB.Data = TraceB;
        foreach (var state in _highlights.Values)
        {
            state.GlowA.Data = TraceA;
            state.TraceA.Data = TraceA;
            state.GlowB.Data = TraceB;
            state.TraceB.Data = TraceB;
        }
    }

    private void ConfigureEdgeMask()
    {
        var fade = Math.Clamp(EdgeFade, 0, 0.49);
        var isCorrupted = TryFindResource("AeziolCorruptedVisuals") is true;
        var corruptionSeed = TryFindResource("AeziolCorruptionSeed") is int seed ? seed : 17;
        var corruption = isCorrupted
            ? new Random(HashCode.Combine(corruptionSeed, _corruptionIdentity))
            : null;
        var edgeFadeBrush = new LinearGradientBrush
        {
            StartPoint = Orientation == JourneyTraceOrientation.Horizontal
                ? new System.Windows.Point(0, 0.5)
                : new System.Windows.Point(0.5, 0),
            EndPoint = Orientation == JourneyTraceOrientation.Horizontal
                ? new System.Windows.Point(1, 0.5)
                : new System.Windows.Point(0.5, 1),
        };
        edgeFadeBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 0));
        edgeFadeBrush.GradientStops.Add(new GradientStop(Colors.White, fade));
        edgeFadeBrush.GradientStops.Add(new GradientStop(Colors.White, 1 - fade));
        edgeFadeBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        TraceRoot.OpacityMask = corruption is null
            ? edgeFadeBrush
            : CorruptionBrushFactory.CreateOpacityMask(
                corruption,
                erosionCount: corruption.Next(3, 7),
                edgeFadeBrush);
    }

    private void RebuildParticles()
    {
        if (!IsInitialized)
        {
            return;
        }

        BaseParticleLayer.Children.Clear();
        foreach (var particle in Particles)
        {
            BaseParticleLayer.Children.Add(CreateParticle(particle, highlighted: false));
        }

        foreach (var state in _highlights.Values)
        {
            RebuildHighlightParticles(state);
        }
    }

    private void RebuildHighlightParticles(HighlightState state)
    {
        state.Particles.Children.Clear();
        foreach (var particle in Particles)
        {
            state.Particles.Children.Add(CreateParticle(particle, highlighted: true));
        }
    }

    private Ellipse CreateParticle(JourneyParticle particle, bool highlighted)
    {
        var displayScale = DisplayScale;
        var size = (highlighted ? particle.HighlightSize : particle.Size) / displayScale;
        var offset = highlighted ? (particle.HighlightSize - particle.Size) / (2 * displayScale) : 0;
        var ellipse = new Ellipse
        {
            Width = size,
            Height = size,
            Opacity = highlighted ? particle.HighlightOpacity : particle.Opacity,
        };
        ellipse.SetResourceReference(
            Shape.FillProperty,
            particle.Tone switch
            {
                JourneyParticleTone.Primary => "AeziolPrimary",
                JourneyParticleTone.Secondary => "AeziolSecondary",
                JourneyParticleTone.BlendedPrimary => "AeziolJourneyParticlePrimary",
                JourneyParticleTone.BlendedSecondary => "AeziolJourneyParticleSecondary",
                _ => "AeziolPrimary",
            });
        Canvas.SetLeft(ellipse, particle.X - offset);
        Canvas.SetTop(ellipse, particle.Y - offset);
        return ellipse;
    }

    private void AnimateHighlight(HighlightState state, bool visible, bool reduceMotion)
    {
        var currentOpacity = state.Layer.Opacity;
        state.Generation++;
        var generation = state.Generation;
        state.Layer.BeginAnimation(OpacityProperty, null);
        var targetOpacity = visible ? 1d : 0d;
        state.Layer.Opacity = targetOpacity;

        if (reduceMotion)
        {
            if (!visible)
            {
                RemoveHighlight(state);
            }

            return;
        }

        var animation = new DoubleAnimation(
            currentOpacity,
            targetOpacity,
            visible ? DefaultHighlightInDuration : DefaultHighlightOutDuration)
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        if (!visible)
        {
            animation.Completed += (_, _) =>
            {
                if (state.Generation == generation && !ReferenceEquals(_highlightOwner, state.Owner))
                {
                    RemoveHighlight(state);
                }
            };
        }

        state.Layer.BeginAnimation(OpacityProperty, animation);
    }

    private void RemoveHighlight(HighlightState state)
    {
        state.Generation++;
        state.Layer.BeginAnimation(OpacityProperty, null);
        HighlightHost.Children.Remove(state.Layer);
        _highlights.Remove(state.Owner);
    }

    private Rect NormalizeRegion(Rect requested)
    {
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return Rect.Empty;
        }

        var left = Math.Clamp(requested.Left, 0, width);
        var top = Math.Clamp(requested.Top, 0, height);
        var right = Math.Clamp(requested.Right, left, width);
        var bottom = Math.Clamp(requested.Bottom, top, height);
        return right <= left || bottom <= top
            ? Rect.Empty
            : new Rect(left, top, right - left, bottom - top);
    }

    private System.Windows.Media.Color FindColor(string key, System.Windows.Media.Color fallback)
        => TryFindResource(key) is System.Windows.Media.Color color ? color : fallback;

    private static LinearGradientBrush CreateGradient(
        System.Windows.Point start,
        System.Windows.Point end,
        System.Windows.Media.Color first,
        System.Windows.Media.Color second)
    {
        var brush = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = start,
            EndPoint = end,
        };
        brush.GradientStops.Add(new GradientStop(first, 0));
        brush.GradientStops.Add(new GradientStop(second, 0.48));
        brush.GradientStops.Add(new GradientStop(first, 1));
        return brush;
    }

    private sealed class HighlightState(
        object owner,
        Canvas layer,
        RectangleGeometry clip,
        LinearGradientBrush mask,
        WpfPath glowA,
        WpfPath glowB,
        WpfPath traceA,
        WpfPath traceB,
        Canvas particles)
    {
        public object Owner { get; } = owner;
        public Canvas Layer { get; } = layer;
        public RectangleGeometry Clip { get; } = clip;
        public LinearGradientBrush Mask { get; } = mask;
        public WpfPath GlowA { get; } = glowA;
        public WpfPath GlowB { get; } = glowB;
        public WpfPath TraceA { get; } = traceA;
        public WpfPath TraceB { get; } = traceB;
        public Canvas Particles { get; } = particles;
        public Rect Region { get; set; }
        public int Generation { get; set; }
    }
}
