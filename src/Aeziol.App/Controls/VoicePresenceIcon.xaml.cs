using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Aeziol.App.Appearance;
using Aeziol.Core.Models;

namespace Aeziol.App.Controls;

public partial class VoicePresenceIcon : System.Windows.Controls.UserControl
{
    internal const int TransitionDurationMilliseconds = 220;
    internal const int StateRotationMotionMilliseconds = 950;
    internal const int StateRotationHoldMilliseconds = 450;

    private VoicePresenceState? _requestedState;
    private VoicePresenceState? _renderedState;
    private int _animatedTransitionCount;
    private int _transitionGeneration;
    private DispatcherTimer? _transitionCleanupTimer;
    private DoubleAnimationUsingKeyFrames? _stateRotationAnimation;
    private bool _transitionInProgress;
    private bool _stateRotationActive;

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

    internal bool HasActiveStateRotation => _stateRotationActive;

    internal DoubleAnimationUsingKeyFrames? ActiveStateRotationAnimation => _stateRotationAnimation;

    internal bool HasAnimatedClocks =>
        CurrentLayer.HasAnimatedProperties
        || PreviousLayer.HasAnimatedProperties
        || CurrentRotation.HasAnimatedProperties
        || PreviousRotation.HasAnimatedProperties;

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property != MotionAssist.IsReducedProperty)
        {
            return;
        }

        if (e.NewValue is true)
        {
            CompleteTransition();
        }
        else if (!_transitionInProgress)
        {
            StartStateRotationIfNeeded();
        }
    }

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs) =>
        ((VoicePresenceIcon)dependencyObject).ShowState((VoicePresenceState)eventArgs.NewValue, animate: true);

    private void ShowState(VoicePresenceState state, bool animate)
    {
        var generation = ++_transitionGeneration;
        var next = VoicePresenceVisual.For(state);
        var previousRequestedState = _requestedState;
        var previousAngle = CurrentRotation.Angle;
        StopStateRotation();
        ClearTransitionAnimations();

        if (previousRequestedState is not null)
        {
            ApplyVisual(PreviousPrimaryPath, PreviousOverlayPath, VoicePresenceVisual.For(previousRequestedState.Value));
        }

        ApplyVisual(CurrentPrimaryPath, CurrentOverlayPath, next);
        _requestedState = state;
        _renderedState = next.State;

        PreviousLayer.Opacity = 0;
        PreviousRotation.Angle = previousAngle;
        CurrentLayer.Opacity = 1;
        CurrentRotation.Angle = 0;

        if (!animate || previousRequestedState is null || MotionAssist.GetIsReduced(this))
        {
            StartStateRotationIfNeeded();
            return;
        }

        _animatedTransitionCount++;
        _transitionInProgress = true;
        var duration = TimeSpan.FromMilliseconds(TransitionDurationMilliseconds);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        BeginTransition(PreviousLayer, OpacityProperty, 1, 0, duration, easing);
        var fadeIn = CreateTransition(0, 1, duration, easing);
        fadeIn.Completed += (_, _) =>
        {
            if (generation == _transitionGeneration)
            {
                CompleteTransition();
            }
        };
        CurrentLayer.BeginAnimation(OpacityProperty, fadeIn);
        ScheduleTransitionCleanup(generation, duration);
    }

    private static void ApplyVisual(
        System.Windows.Shapes.Path primaryPath,
        System.Windows.Shapes.Path overlayPath,
        VoicePresenceVisual visual)
    {
        primaryPath.Data = visual.PrimaryGeometry;
        overlayPath.Data = visual.OverlayGeometry;
        primaryPath.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, visual.FillBrushKey);
        overlayPath.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, visual.FillBrushKey);
    }

    private static void BeginTransition(
        System.Windows.Controls.Viewbox target,
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
                CompleteTransition();
            }
        };
        _transitionCleanupTimer.Start();
    }

    private void CompleteTransition()
    {
        _transitionGeneration++;
        ClearTransitionAnimations();
        StopStateRotation();
        PreviousLayer.Opacity = 0;
        PreviousRotation.Angle = 0;
        CurrentLayer.Opacity = 1;
        CurrentRotation.Angle = 0;
        StartStateRotationIfNeeded();
    }

    private void StartStateRotationIfNeeded()
    {
        StopStateRotation();
        if (_requestedState is null
            || !VoicePresenceVisual.Rotates(_requestedState.Value)
            || MotionAssist.GetIsReduced(this))
        {
            return;
        }

        _stateRotationAnimation = new DoubleAnimationUsingKeyFrames
        {
            RepeatBehavior = RepeatBehavior.Forever,
            KeyFrames =
            {
                new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                new EasingDoubleKeyFrame(
                    360,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(StateRotationMotionMilliseconds)),
                    new SineEase { EasingMode = EasingMode.EaseInOut }),
                new DiscreteDoubleKeyFrame(
                    360,
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(
                        StateRotationMotionMilliseconds + StateRotationHoldMilliseconds))),
            },
        };
        CurrentRotation.BeginAnimation(RotateTransform.AngleProperty, _stateRotationAnimation);
        _stateRotationActive = true;
    }

    private void StopStateRotation()
    {
        CurrentRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        CurrentRotation.Angle = 0;
        _stateRotationAnimation = null;
        _stateRotationActive = false;
    }

    private void ClearTransitionAnimations()
    {
        _transitionCleanupTimer?.Stop();
        _transitionCleanupTimer = null;
        _transitionInProgress = false;
        PreviousLayer.BeginAnimation(OpacityProperty, null);
        CurrentLayer.BeginAnimation(OpacityProperty, null);
    }
}
