using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Xml.Linq;
using Aeziol.App.Appearance;
using Aeziol.App.Controls;
using Aeziol.Core.Models;

namespace Aeziol.Tests.App;

[Collection(WpfUiTestGroup.Name)]
public sealed class VoicePresenceIconTests
{
    [Fact]
    public void CatalogMapsNineRuntimeStatesToFiveVersionedVisualAssets()
    {
        var expectedAssets = new[]
        {
            "discord-absent.svg",
            "discord-authorization-required.svg",
            "discord-connected.svg",
            "discord-out-of-voice.svg",
            "discord-unavailable.svg",
        };

        Assert.Equal(5, VoicePresenceVisual.All.Count);
        Assert.Equal(
            expectedAssets,
            VoicePresenceVisual.All.Select(visual => visual.AssetFileName).Order(StringComparer.Ordinal));
        Assert.All(Enum.GetValues<VoicePresenceState>(), state => Assert.NotNull(VoicePresenceVisual.For(state)));

        var outOfVoice = VoicePresenceVisual.For(VoicePresenceState.OutOfVoice);
        Assert.Same(outOfVoice, VoicePresenceVisual.For(VoicePresenceState.Connecting));
        Assert.Same(outOfVoice, VoicePresenceVisual.For(VoicePresenceState.ChangingChannel));
        Assert.Same(outOfVoice, VoicePresenceVisual.For(VoicePresenceState.Reconnecting));
        Assert.Same(outOfVoice, VoicePresenceVisual.For(VoicePresenceState.Disconnected));
        Assert.Equal("status-out-of-voice", VoicePresenceVisual.LocalizationKeyFor(VoicePresenceState.Disconnected));

        var assetDirectory = FindSourceDirectory("src", "Aeziol.App", "Assets", "VoicePresence");
        Assert.Equal(
            expectedAssets,
            Directory.GetFiles(assetDirectory, "*.svg").Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SvgSourcesMatchTheFiveWpfFillGeometries()
    {
        var assetDirectory = FindSourceDirectory("src", "Aeziol.App", "Assets", "VoicePresence");

        foreach (var visual in VoicePresenceVisual.All)
        {
            var document = XDocument.Load(Path.Combine(assetDirectory, visual.AssetFileName));
            var root = Assert.Single(document.Elements());
            var primary = Assert.Single(document.Descendants(), element => element.Attribute("data-role")?.Value == "primary");
            var cutouts = document.Descendants()
                .Where(element => element.Attribute("data-role")?.Value == "cutout")
                .ToArray();
            var overlays = document.Descendants()
                .Where(element => element.Attribute("data-role")?.Value == "overlay")
                .ToArray();

            Assert.Equal("65", root.Attribute("width")?.Value);
            Assert.Equal("48", root.Attribute("height")?.Value);
            Assert.Equal("0 0 65 48", root.Attribute("viewBox")?.Value);
            Assert.Equal(visual.PrimaryPathData, primary.Attribute("d")?.Value);
            Assert.Equal(visual.CutoutPathData, cutouts.SingleOrDefault()?.Attribute("d")?.Value);
            Assert.Equal(visual.OverlayPathData, overlays.SingleOrDefault()?.Attribute("d")?.Value);
            Assert.NotEmpty(visual.PrimaryGeometry.GetFlattenedPathGeometry().Figures);
            Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName is "image" or "canvas");
            Assert.DoesNotContain(document.Descendants().Attributes(), attribute =>
                attribute.Name.LocalName.StartsWith("stroke", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void StateShapesFollowTheAuthoritativeMapping()
    {
        var brandDocument = XDocument.Load(FindSourceFile("src", "Aeziol.App", "Assets", "Brand", "discord-symbol.svg"));
        var brandPath = Assert.Single(brandDocument.Descendants(), element => element.Name.LocalName == "path")
            .Attribute("d")?.Value;
        var assetDirectory = FindSourceDirectory("src", "Aeziol.App", "Assets", "VoicePresence");

        var outOfVoice = VoicePresenceVisual.For(VoicePresenceState.OutOfVoice);
        Assert.Equal(brandPath, outOfVoice.PrimaryPathData);
        Assert.Null(outOfVoice.CutoutPathData);
        Assert.Null(outOfVoice.OverlayPathData);

        var authorization = VoicePresenceVisual.For(VoicePresenceState.AuthorizationRequired);
        Assert.Equal(brandPath, authorization.PrimaryPathData);
        Assert.Null(authorization.CutoutPathData);
        Assert.Null(authorization.OverlayPathData);
        Assert.Equal("AeziolMuted", authorization.FillBrushKey);

        var absent = VoicePresenceVisual.For(VoicePresenceState.DiscordAbsent);
        Assert.Equal(brandPath, absent.PrimaryPathData);
        Assert.Equal(VoicePresenceVisual.DiscordCracksPathData, absent.CutoutPathData);
        Assert.True(GeometryFigureCount(absent.CutoutPathData) >= 3);

        var connected = VoicePresenceVisual.For(VoicePresenceState.Connected);
        Assert.Equal(VoicePresenceVisual.SpeakerPathData, connected.PrimaryPathData);
        Assert.True(GeometryFigureCount(connected.PrimaryPathData) >= 3);
        var connectedDocument = XDocument.Load(Path.Combine(assetDirectory, connected.AssetFileName));
        Assert.Equal("speaker", Assert.Single(connectedDocument.Descendants(), element =>
            element.Attribute("data-role")?.Value == "primary").Attribute("data-shape")?.Value);

        var unavailable = VoicePresenceVisual.For(VoicePresenceState.Unavailable);
        Assert.Equal(VoicePresenceVisual.QuestionMarkPathData, unavailable.PrimaryPathData);
        Assert.True(GeometryFigureCount(unavailable.PrimaryPathData) >= 2);
        var unavailableDocument = XDocument.Load(Path.Combine(assetDirectory, unavailable.AssetFileName));
        Assert.Equal("question-mark", Assert.Single(unavailableDocument.Descendants(), element =>
            element.Attribute("data-role")?.Value == "primary").Attribute("data-shape")?.Value);
    }

    [Fact]
    public void ControlCrossfadesEveryRuntimeStateAndNormalizesDisconnected()
    {
        WpfTestHost.Run(() =>
        {
            var icon = new VoicePresenceIcon();
            Assert.Equal(VoicePresenceState.DiscordAbsent, icon.RenderedState);

            foreach (var state in Enum.GetValues<VoicePresenceState>().Skip(1))
            {
                icon.State = state;
                Assert.Equal(VoicePresenceVisual.For(state).State, icon.RenderedState);
                Assert.Equal(VoicePresenceVisual.For(state).AssetFileName, icon.RenderedAssetFileName);
            }

            Assert.Equal(Enum.GetValues<VoicePresenceState>().Length - 1, icon.AnimatedTransitionCount);
            Assert.Equal(VoicePresenceState.Unavailable, icon.RenderedState);
        });

        var document = XDocument.Load(FindSourceFile("src", "Aeziol.App", "Controls", "VoicePresenceIcon.xaml"));
        Assert.Equal(2, document.Descendants().Count(element => element.Name.LocalName == "Viewbox"));
        Assert.Equal(4, document.Descendants().Count(element => element.Name.LocalName == "Path"));
        Assert.All(document.Descendants().Where(element => element.Name.LocalName == "Path"), path =>
        {
            Assert.NotNull(path.Attribute("Fill"));
            Assert.Null(path.Attribute("Stroke"));
        });
    }

    [Fact]
    public void OnlyTransientStatesUseAnEasedTurnAndVisibleHoldCycle()
    {
        var expectedRotatingStates = new[]
        {
            VoicePresenceState.Connecting,
            VoicePresenceState.ChangingChannel,
            VoicePresenceState.Reconnecting,
        };
        Assert.Equal(
            expectedRotatingStates,
            Enum.GetValues<VoicePresenceState>().Where(VoicePresenceVisual.Rotates));

        WpfTestHost.Run(() =>
        {
            var icon = new VoicePresenceIcon { State = VoicePresenceState.Connecting };
            PumpDispatcher(TimeSpan.FromMilliseconds(VoicePresenceIcon.TransitionDurationMilliseconds + 80));

            Assert.False(icon.HasActiveTransition);
            Assert.True(icon.HasActiveStateRotation);
            var animation = Assert.IsType<DoubleAnimationUsingKeyFrames>(icon.ActiveStateRotationAnimation);
            Assert.Equal(RepeatBehavior.Forever, animation.RepeatBehavior);
            Assert.Collection(
                animation.KeyFrames.Cast<DoubleKeyFrame>().ToArray(),
                first =>
                {
                    Assert.IsType<LinearDoubleKeyFrame>(first);
                    Assert.Equal(0, first.Value);
                    Assert.Equal(TimeSpan.Zero, first.KeyTime.TimeSpan);
                },
                turn =>
                {
                    var eased = Assert.IsType<EasingDoubleKeyFrame>(turn);
                    Assert.Equal(360, eased.Value);
                    Assert.Equal(
                        TimeSpan.FromMilliseconds(VoicePresenceIcon.StateRotationMotionMilliseconds),
                        eased.KeyTime.TimeSpan);
                    Assert.IsType<SineEase>(eased.EasingFunction);
                },
                hold =>
                {
                    Assert.IsType<DiscreteDoubleKeyFrame>(hold);
                    Assert.Equal(360, hold.Value);
                    Assert.Equal(
                        TimeSpan.FromMilliseconds(
                            VoicePresenceIcon.StateRotationMotionMilliseconds
                            + VoicePresenceIcon.StateRotationHoldMilliseconds),
                        hold.KeyTime.TimeSpan);
                });
            Assert.InRange(VoicePresenceIcon.StateRotationMotionMilliseconds, 850, 1000);
            Assert.InRange(VoicePresenceIcon.StateRotationHoldMilliseconds, 350, 550);
        });
    }

    [Fact]
    public void RotationStopsImmediatelyOnStableStateRapidChangeAndReducedMotion()
    {
        WpfTestHost.Run(() =>
        {
            var icon = new VoicePresenceIcon { State = VoicePresenceState.Connecting };
            PumpDispatcher(TimeSpan.FromMilliseconds(VoicePresenceIcon.TransitionDurationMilliseconds + 80));
            Assert.True(icon.HasActiveStateRotation);

            icon.State = VoicePresenceState.Connected;
            Assert.False(icon.HasActiveStateRotation);
            Assert.Null(icon.ActiveStateRotationAnimation);

            icon.State = VoicePresenceState.Reconnecting;
            MotionAssist.SetIsReduced(icon, true);
            Assert.False(icon.HasActiveTransition);
            Assert.False(icon.HasActiveStateRotation);
            Assert.False(icon.HasAnimatedClocks);

            icon.State = VoicePresenceState.ChangingChannel;
            Assert.Equal(VoicePresenceState.OutOfVoice, icon.RenderedState);
            Assert.False(icon.HasActiveStateRotation);
            Assert.False(icon.HasAnimatedClocks);

            MotionAssist.SetIsReduced(icon, false);
            Assert.True(icon.HasActiveStateRotation);
            MotionAssist.SetIsReduced(icon, true);
            Assert.False(icon.HasActiveStateRotation);
            Assert.False(icon.HasAnimatedClocks);
        });
    }

    [Fact]
    public void CompletedStableTransitionCleansAllAnimationClocksAfterRapidChanges()
    {
        WpfTestHost.Run(() =>
        {
            var icon = new VoicePresenceIcon();
            icon.State = VoicePresenceState.Connecting;
            icon.State = VoicePresenceState.Connected;
            icon.State = VoicePresenceState.Unavailable;

            PumpDispatcher(TimeSpan.FromMilliseconds(VoicePresenceIcon.TransitionDurationMilliseconds + 80));

            Assert.Equal(VoicePresenceState.Unavailable, icon.RenderedState);
            Assert.False(icon.HasActiveTransition);
            Assert.False(icon.HasActiveStateRotation);
            Assert.False(icon.HasAnimatedClocks);
        });
    }

    private static int GeometryFigureCount(string? pathData) =>
        pathData is null ? 0 : System.Windows.Media.Geometry.Parse(pathData).GetFlattenedPathGeometry().Figures.Count;

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            frame.Continue = false;
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static string FindSourceDirectory(params string[] relativeSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeSegments]);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate {Path.Combine(relativeSegments)} from the test output directory.");
    }

    private static string FindSourceFile(params string[] relativeSegments)
    {
        var directory = FindSourceDirectory(relativeSegments[..^1]);
        var candidate = Path.Combine(directory, relativeSegments[^1]);
        return File.Exists(candidate)
            ? candidate
            : throw new FileNotFoundException($"Could not locate {candidate}.");
    }
}
