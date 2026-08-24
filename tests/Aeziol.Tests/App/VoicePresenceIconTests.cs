using System.Xml.Linq;
using System.Windows.Threading;
using Aeziol.App.Appearance;
using Aeziol.App.Controls;
using Aeziol.Core.Models;

namespace Aeziol.Tests.App;

[Collection(WpfUiTestGroup.Name)]
public sealed class VoicePresenceIconTests
{
    [Fact]
    public void CatalogMapsEveryVoiceStateToAUniqueVersionedSvg()
    {
        var expectedStates = Enum.GetValues<VoicePresenceState>();
        var visuals = VoicePresenceVisual.All;

        Assert.Equal(expectedStates.Length, visuals.Count);
        Assert.Equal(expectedStates.Order(), visuals.Select(visual => visual.State).Order());
        Assert.Equal(visuals.Count, visuals.Select(visual => visual.AssetFileName).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(visuals.Count, visuals.Select(visual => visual.PathData).Distinct(StringComparer.Ordinal).Count());

        var assetDirectory = FindSourceDirectory("src", "Aeziol.App", "Assets", "VoicePresence");
        var svgFiles = Directory.GetFiles(assetDirectory, "*.svg", SearchOption.TopDirectoryOnly);
        Assert.Equal(expectedStates.Length, svgFiles.Length);
        Assert.Equal(
            visuals.Select(visual => visual.AssetFileName).Order(StringComparer.Ordinal),
            svgFiles.Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void SvgSourcesMatchTheWpfGeometryCatalog()
    {
        var assetDirectory = FindSourceDirectory("src", "Aeziol.App", "Assets", "VoicePresence");

        foreach (var visual in VoicePresenceVisual.All)
        {
            var document = XDocument.Load(Path.Combine(assetDirectory, visual.AssetFileName));
            var root = Assert.Single(document.Elements());
            var path = Assert.Single(root.Elements(), element => element.Name.LocalName == "path");

            Assert.Equal("svg", root.Name.LocalName);
            Assert.Equal("72", root.Attribute("width")?.Value);
            Assert.Equal("56", root.Attribute("height")?.Value);
            Assert.Equal("0 0 72 56", root.Attribute("viewBox")?.Value);
            Assert.Equal("none", root.Attribute("fill")?.Value);
            Assert.Equal(visual.PathData, path.Attribute("d")?.Value);
            Assert.Equal("#5865F2", path.Attribute("stroke")?.Value);
            Assert.Equal("2.6", path.Attribute("stroke-width")?.Value);
            Assert.Equal("round", path.Attribute("stroke-linecap")?.Value);
            Assert.Equal("round", path.Attribute("stroke-linejoin")?.Value);
            Assert.NotEmpty(visual.Geometry.GetFlattenedPathGeometry().Figures);
            Assert.DoesNotContain(document.Descendants(), element =>
                element.Name.LocalName is "image" or "canvas");
        }
    }

    [Fact]
    public void ControlUsesTwoVectorLayersAndAnimatesEveryStateChange()
    {
        WpfTestHost.Run(() =>
        {
            var icon = new VoicePresenceIcon();

            Assert.Equal(VoicePresenceState.DiscordAbsent, icon.RenderedState);
            Assert.Equal("discord-absent.svg", icon.RenderedAssetFileName);
            Assert.Equal(0, icon.AnimatedTransitionCount);

            foreach (var state in Enum.GetValues<VoicePresenceState>().Skip(1))
            {
                icon.State = state;
                Assert.Equal(state, icon.RenderedState);
                Assert.Equal(VoicePresenceVisual.For(state).AssetFileName, icon.RenderedAssetFileName);
            }

            Assert.Equal(Enum.GetValues<VoicePresenceState>().Length - 1, icon.AnimatedTransitionCount);
            Assert.True(icon.HasActiveTransition);
        });

        var document = XDocument.Load(FindSourceFile("src", "Aeziol.App", "Controls", "VoicePresenceIcon.xaml"));
        var layers = document.Descendants()
            .Where(element => element.Name.LocalName == "Viewbox")
            .ToArray();
        var paths = document.Descendants()
            .Where(element => element.Name.LocalName == "Path")
            .ToArray();

        Assert.Equal(2, layers.Length);
        Assert.Equal(2, paths.Length);
        Assert.All(paths, path => Assert.Equal("2.6", path.Attribute("StrokeThickness")?.Value));
        Assert.DoesNotContain(document.Descendants(), element =>
            element.Name.LocalName is "Image" or "Canvas");
    }

    [Fact]
    public void ReducedMotionMakesStateChangesInstantAndStopsAnActiveTransition()
    {
        WpfTestHost.Run(() =>
        {
            var icon = new VoicePresenceIcon();
            MotionAssist.SetIsReduced(icon, true);

            icon.State = VoicePresenceState.Connected;

            Assert.Equal(VoicePresenceState.Connected, icon.RenderedState);
            Assert.Equal(0, icon.AnimatedTransitionCount);
            Assert.False(icon.HasActiveTransition);

            MotionAssist.SetIsReduced(icon, false);
            icon.State = VoicePresenceState.Reconnecting;
            Assert.Equal(1, icon.AnimatedTransitionCount);
            Assert.True(icon.HasActiveTransition);

            MotionAssist.SetIsReduced(icon, true);
            Assert.False(icon.HasActiveTransition);
        });
    }

    [Fact]
    public void TransientStatesUseDistinctVectorEntryMotions()
    {
        WpfTestHost.Run(() =>
        {
            var icon = new VoicePresenceIcon();

            icon.State = VoicePresenceState.OutOfVoice;
            var settle = icon.LastEntryPose;
            Assert.Equal(VoicePresenceEntryMotion.Settle, icon.LastEntryMotion);
            Assert.Equal(0.88, settle.ScaleX);
            Assert.Equal(2.5, settle.Y);

            icon.State = VoicePresenceState.Connecting;
            var converge = icon.LastEntryPose;
            Assert.Equal(VoicePresenceEntryMotion.Converge, icon.LastEntryMotion);
            Assert.Equal(0.68, converge.ScaleX);
            Assert.Equal(0, converge.X);

            icon.State = VoicePresenceState.ChangingChannel;
            var slide = icon.LastEntryPose;
            Assert.Equal(VoicePresenceEntryMotion.Slide, icon.LastEntryMotion);
            Assert.Equal(1, slide.ScaleX);
            Assert.Equal(-6, slide.X);

            icon.State = VoicePresenceState.Reconnecting;
            var returning = icon.LastEntryPose;
            Assert.Equal(VoicePresenceEntryMotion.Return, icon.LastEntryMotion);
            Assert.Equal(-2, returning.X);
            Assert.Equal(-9, returning.Angle);

            Assert.Equal(4, new[] { settle, converge, slide, returning }.Distinct().Count());
        });
    }

    [Fact]
    public void CompletedTransitionCleansAnimationClocksAfterRapidChanges()
    {
        WpfTestHost.Run(() =>
        {
            var icon = new VoicePresenceIcon();
            icon.State = VoicePresenceState.Connecting;
            Assert.True(icon.HasActiveTransition);

            icon.State = VoicePresenceState.ChangingChannel;
            Assert.Equal(VoicePresenceState.ChangingChannel, icon.RenderedState);
            Assert.True(icon.HasActiveTransition);

            PumpDispatcher(TimeSpan.FromMilliseconds(VoicePresenceIcon.TransitionDurationMilliseconds + 80));

            Assert.Equal(VoicePresenceState.ChangingChannel, icon.RenderedState);
            Assert.False(icon.HasActiveTransition);
            Assert.False(icon.HasAnimatedClocks);
            Assert.Equal(2, icon.AnimatedTransitionCount);
        });
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = duration,
        };
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
