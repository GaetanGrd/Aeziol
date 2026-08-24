using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Threading;
using Aeziol.App.Appearance;
using Aeziol.Core.Models;

namespace Aeziol.App.Controls;

public partial class VoicePresenceIcon : System.Windows.Controls.UserControl
{
    internal const int TransitionDurationMilliseconds = 220;

    private VoicePresenceState? _renderedState;
    private int _animatedTransitionCount;
    private int _transitionGeneration;
    private DispatcherTimer? _transitionCleanupTimer;
    private bool _transitionInProgress;

    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(VoicePresenceState),
        typeof(VoicePresenceIcon),
        new FrameworkPropertyMetadata(VoicePresenceState.DiscordAbsent, OnStateChanged));

    public VoicePresenceIcon()
    {
        InitializeComponent();
        ShowState(State, animate: false);
    }

    public VoicePresenceState State
    {
        get => (VoicePresenceState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    internal VoicePresenceState? RenderedState => _renderedState;

    internal string RenderedAssetFileName => VoicePresenceVisual.For(State).AssetFileName;

    internal int AnimatedTransitionCount => _animatedTransitionCount;

    internal bool HasActiveTransition => _transitionInProgress;

    internal bool HasAnimatedClocks =>
        CurrentLayer.HasAnimatedProperties
        || PreviousLayer.HasAnimatedProperties
        || CurrentScale.HasAnimatedProperties
        || CurrentRotation.HasAnimatedProperties
        || CurrentTranslation.HasAnimatedProperties
        || PreviousScale.HasAnimatedProperties;

    internal VoicePresenceEntryMotion LastEntryMotion { get; private set; }

    internal VoicePresenceTransitionPose LastEntryPose { get; private set; }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == MotionAssist.IsReducedProperty && e.NewValue is true)
        {
            FinishTransition();
        }
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs) =>
        ((VoicePresenceIcon)dependencyObject).ShowState((VoicePresenceState)eventArgs.NewValue, animate: true);

    private void ShowState(VoicePresenceState state, bool animate)
    {
        var generation = ++_transitionGeneration;
        var next = VoicePresenceVisual.For(state);
        var previousState = _renderedState;
        ClearTransitionAnimations();

        if (previousState is not null)
        {
            var previous = VoicePresenceVisual.For(previousState.Value);
            PreviousPath.Data = previous.Geometry;
            PreviousPath.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, previous.StrokeBrushKey);
        }

        CurrentPath.Data = next.Geometry;
        CurrentPath.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, next.StrokeBrushKey);
        _renderedState = state;

        PreviousLayer.Opacity = 0;
        PreviousScale.ScaleX = 1;
        PreviousScale.ScaleY = 1;
        CurrentLayer.Opacity = 1;
        CurrentScale.ScaleX = 1;
        CurrentScale.ScaleY = 1;
        CurrentRotation.Angle = 0;
        CurrentTranslation.X = 0;
        CurrentTranslation.Y = 0;
        LastEntryMotion = next.EntryMotion;
        LastEntryPose = ResolveEntryPose(next.EntryMotion);

        if (!animate || previousState is null || previousState == state || MotionAssist.GetIsReduced(this))
        {
            return;
        }

        _animatedTransitionCount++;
        _transitionInProgress = true;
        var duration = TimeSpan.FromMilliseconds(TransitionDurationMilliseconds);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginTransition(PreviousLayer, OpacityProperty, 1, 0, duration, easing);
        BeginTransition(PreviousScale, ScaleTransform.ScaleXProperty, 1, 1.08, duration, easing);
        BeginTransition(PreviousScale, ScaleTransform.ScaleYProperty, 1, 1.08, duration, easing);
        var fadeIn = CreateTransition(0, 1, duration, easing);
        fadeIn.Completed += (_, _) =>
        {
            if (generation == _transitionGeneration)
            {
                FinishTransition();
            }
        };
        CurrentLayer.BeginAnimation(OpacityProperty, fadeIn);
        BeginTransition(CurrentScale, ScaleTransform.ScaleXProperty, LastEntryPose.ScaleX, 1, duration, easing);
        BeginTransition(CurrentScale, ScaleTransform.ScaleYProperty, LastEntryPose.ScaleY, 1, duration, easing);
        BeginTransition(CurrentRotation, RotateTransform.AngleProperty, LastEntryPose.Angle, 0, duration, easing);
        BeginTransition(CurrentTranslation, TranslateTransform.XProperty, LastEntryPose.X, 0, duration, easing);
        BeginTransition(CurrentTranslation, TranslateTransform.YProperty, LastEntryPose.Y, 0, duration, easing);
        ScheduleTransitionCleanup(generation, duration);
    }

    private static void BeginTransition(
        IAnimatable target,
        DependencyProperty property,
        double from,
        double to,
        TimeSpan duration,
        IEasingFunction easing) =>
        target.BeginAnimation(property, CreateTransition(from, to, duration, easing));

    private static DoubleAnimation CreateTransition(
        double from,
        double to,
        TimeSpan duration,
        IEasingFunction easing) =>
        new(from, to, duration)
        {
            EasingFunction = easing,
            FillBehavior = FillBehavior.Stop,
        };

    private static VoicePresenceTransitionPose ResolveEntryPose(VoicePresenceEntryMotion motion) => motion switch
    {
        VoicePresenceEntryMotion.Converge => new(0.68, 0.96, 0, 0, 0),
        VoicePresenceEntryMotion.Slide => new(1, 1, -6, 0, 0),
        VoicePresenceEntryMotion.Return => new(0.9, 0.9, -2, 2, -9),
        _ => new(0.88, 0.88, 0, 2.5, 0),
    };

    private void ScheduleTransitionCleanup(int generation, TimeSpan duration)
    {
        _transitionCleanupTimer?.Stop();
        _transitionCleanupTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = duration,
        };
        _transitionCleanupTimer.Tick += (_, _) =>
        {
            _transitionCleanupTimer?.Stop();
            _transitionCleanupTimer = null;
            if (generation == _transitionGeneration)
            {
                FinishTransition();
            }
        };
        _transitionCleanupTimer.Start();
    }

    private void FinishTransition()
    {
        _transitionGeneration++;
        ClearTransitionAnimations();
        PreviousLayer.Opacity = 0;
        PreviousScale.ScaleX = 1;
        PreviousScale.ScaleY = 1;
        CurrentLayer.Opacity = 1;
        CurrentScale.ScaleX = 1;
        CurrentScale.ScaleY = 1;
        CurrentRotation.Angle = 0;
        CurrentTranslation.X = 0;
        CurrentTranslation.Y = 0;
    }

    private void ClearTransitionAnimations()
    {
        _transitionCleanupTimer?.Stop();
        _transitionCleanupTimer = null;
        _transitionInProgress = false;
        PreviousLayer.BeginAnimation(OpacityProperty, null);
        PreviousScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        PreviousScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        CurrentLayer.BeginAnimation(OpacityProperty, null);
        CurrentScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        CurrentScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        CurrentRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        CurrentTranslation.BeginAnimation(TranslateTransform.XProperty, null);
        CurrentTranslation.BeginAnimation(TranslateTransform.YProperty, null);
    }
}

internal readonly record struct VoicePresenceTransitionPose(
    double ScaleX,
    double ScaleY,
    double X,
    double Y,
    double Angle);
