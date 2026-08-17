using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Aeziol.App.Controls;
using WpfSize = System.Windows.Size;

namespace Aeziol.Tests.App;

[Collection(WpfUiTestGroup.Name)]
public sealed class JourneyTraceTests
{
    [Fact]
    public void ReusableTrace_RendersParticlesAndCrossfadesBetweenRegions()
    {
        Exception? failure = null;
        WpfTestHost.Run(() =>
        {
            try
            {
                var trace = new JourneyTrace
                {
                    Width = 200,
                    Height = 62,
                    Orientation = JourneyTraceOrientation.Horizontal,
                    TraceA = Geometry.Parse("M 0,35 C 50,0 120,62 200,31"),
                    TraceB = Geometry.Parse("M 0,31 C 55,8 125,55 200,35"),
                };
                trace.Particles.Add(new JourneyParticle
                {
                    X = 40,
                    Y = 12,
                    Size = 2.5,
                    HighlightSize = 3.5,
                    Tone = JourneyParticleTone.Primary,
                });
                trace.Particles.Add(new JourneyParticle
                {
                    X = 130,
                    Y = 48,
                    Size = 2,
                    HighlightSize = 3,
                    Tone = JourneyParticleTone.Secondary,
                });

                var window = new Window
                {
                    Width = 240,
                    Height = 100,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Opacity = 0,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -32000,
                    Top = -32000,
                    Content = trace,
                };
                window.Show();
                trace.UpdateLayout();

                Assert.Equal(2, trace.RenderedBaseParticleCount);
                Assert.Equal(2.5, trace.RenderedBaseParticleWidth);
                trace.DisplayScale = 2;
                Assert.Equal(1.25, trace.RenderedBaseParticleWidth);
                Assert.Equal(2.5, trace.RenderedBaseParticleWidth * trace.DisplayScale);
                Assert.False(trace.UsesSpottedCorruptionMask);
                trace.Resources["AeziolCorruptedVisuals"] = true;
                trace.RefreshPalette();
                Assert.True(trace.UsesSpottedCorruptionMask);
                trace.Resources["AeziolCorruptedVisuals"] = false;
                trace.RefreshPalette();

                var firstRow = new object();
                var secondRow = new object();
                var firstRegion = new Rect(0, 0, 90, 62);
                trace.ShowHighlight(firstRow, firstRegion, reduceMotion: true);
                Assert.Equal(firstRegion, trace.CurrentHighlightRect);
                Assert.Equal(1, trace.HighlightOpacity);

                var secondRegion = new Rect(88, 0, 112, 62);
                trace.ShowHighlight(secondRow, secondRegion, reduceMotion: false);
                Assert.Equal(secondRegion, trace.CurrentHighlightRect);
                Assert.Equal(1, trace.OutgoingHighlightCount);

                trace.HideHighlight(reduceMotion: true);
                Assert.Equal(0, trace.HighlightOpacity);
                window.Close();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        Assert.Null(failure);
    }

    [Fact]
    public void ReusableTrace_ReusesTwoVectorLayersDuringRepeatedAlternation()
    {
        Exception? failure = null;
        WpfTestHost.Run(() =>
        {
            try
            {
                var firstRow = new object();
                var secondRow = new object();
                var firstRegion = new Rect(0, 0, 30, 50);
                var secondRegion = new Rect(0, 60, 30, 50);
                var trace = new JourneyTrace
                {
                    Width = 30,
                    Height = 120,
                    Orientation = JourneyTraceOrientation.Vertical,
                    TraceA = Geometry.Parse("M 8,0 C 22,30 2,85 19,120"),
                    TraceB = Geometry.Parse("M 15,0 C 1,38 26,78 10,120"),
                };
                var window = new Window
                {
                    Width = 80,
                    Height = 160,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Opacity = 0,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -32000,
                    Top = -32000,
                    Content = trace,
                };
                window.Show();
                trace.UpdateLayout();

                trace.ShowHighlight(firstRow, firstRegion, reduceMotion: false);
                PumpDispatcher(TimeSpan.FromMilliseconds(55));
                trace.ShowHighlight(secondRow, secondRegion, reduceMotion: false);

                for (var index = 0; index < 8; index++)
                {
                    PumpDispatcher(TimeSpan.FromMilliseconds(55));
                    var showFirst = index % 2 == 0;
                    trace.ShowHighlight(
                        showFirst ? firstRow : secondRow,
                        showFirst ? firstRegion : secondRegion,
                        reduceMotion: false);

                    Assert.Equal(2, trace.HighlightLayerCount);
                    Assert.Equal(1, trace.OutgoingHighlightCount);
                    Assert.Equal(showFirst ? firstRegion : secondRegion, trace.CurrentHighlightRect);
                    Assert.True(trace.HighlightOpacity > 0);
                }

                trace.HideHighlight(reduceMotion: false);
                WaitUntil(() => trace.HighlightLayerCount == 0, TimeSpan.FromSeconds(2));
                window.Close();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        Assert.Null(failure);
    }

    [Fact]
    public void ReusableTrace_ClampsHighlightToItsBounds()
    {
        Exception? failure = null;
        WpfTestHost.Run(() =>
        {
            try
            {
                var trace = new JourneyTrace
                {
                    Width = 19,
                    Height = 200,
                    Orientation = JourneyTraceOrientation.Vertical,
                    TraceA = Geometry.Parse("M 7,0 C 4,45 14,150 9,200"),
                    TraceB = Geometry.Parse("M 11,0 C 7,50 16,145 12,200"),
                };
                trace.Measure(new WpfSize(19, 200));
                trace.Arrange(new Rect(0, 0, 19, 200));
                trace.UpdateLayout();

                trace.ShowHighlight(new Rect(-4, 180, 30, 40), reduceMotion: true);

                Assert.Equal(new Rect(0, 180, 19, 20), trace.CurrentHighlightRect);
                Assert.Equal(1, trace.HighlightOpacity);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        Assert.Null(failure);
    }

    [Fact]
    public void ReusableTrace_KeepsEveryOutgoingFadeAliveDuringRapidHovering()
    {
        Exception? failure = null;
        WpfTestHost.Run(() =>
        {
            try
            {
                var trace = new JourneyTrace
                {
                    Width = 30,
                    Height = 240,
                    Orientation = JourneyTraceOrientation.Vertical,
                    TraceA = Geometry.Parse("M 8,0 C 22,60 2,170 19,240"),
                    TraceB = Geometry.Parse("M 15,0 C 1,75 26,155 10,240"),
                };
                var window = new Window
                {
                    Width = 80,
                    Height = 280,
                    WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Opacity = 0,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -32000,
                    Top = -32000,
                    Content = trace,
                };
                window.Show();
                trace.UpdateLayout();

                var firstRow = new object();
                var secondRow = new object();
                var thirdRow = new object();
                var fourthRow = new object();
                trace.ShowHighlight(firstRow, new Rect(0, 0, 30, 55), reduceMotion: false);
                PumpDispatcher(TimeSpan.FromMilliseconds(55));
                trace.ShowHighlight(secondRow, new Rect(0, 55, 30, 55), reduceMotion: false);
                Assert.Equal(1, trace.OutgoingHighlightCount);

                PumpDispatcher(TimeSpan.FromMilliseconds(55));
                trace.ShowHighlight(thirdRow, new Rect(0, 110, 30, 55), reduceMotion: false);
                Assert.Equal(2, trace.OutgoingHighlightCount);

                PumpDispatcher(TimeSpan.FromMilliseconds(55));
                trace.ShowHighlight(fourthRow, new Rect(0, 165, 30, 55), reduceMotion: false);
                Assert.Equal(3, trace.OutgoingHighlightCount);

                WaitUntil(() => trace.OutgoingHighlightCount == 0, TimeSpan.FromSeconds(2));
                window.Close();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        Assert.Null(failure);
    }

    [Fact]
    public void ReusableTrace_IgnoresALateLeaveFromThePreviousHoveredRow()
    {
        Exception? failure = null;
        WpfTestHost.Run(() =>
        {
            try
            {
                var firstRow = new object();
                var secondRow = new object();
                var trace = new JourneyTrace
                {
                    Width = 30,
                    Height = 120,
                    Orientation = JourneyTraceOrientation.Vertical,
                };
                trace.Measure(new WpfSize(30, 120));
                trace.Arrange(new Rect(0, 0, 30, 120));
                trace.UpdateLayout();

                trace.ShowHighlight(firstRow, new Rect(0, 0, 30, 50), reduceMotion: true);
                trace.ShowHighlight(secondRow, new Rect(0, 60, 30, 50), reduceMotion: true);

                trace.HideHighlight(firstRow, reduceMotion: true);

                Assert.Equal(1, trace.HighlightOpacity);
                Assert.Equal(new Rect(0, 60, 30, 50), trace.CurrentHighlightRect);

                trace.HideHighlight(secondRow, reduceMotion: true);
                Assert.Equal(0, trace.HighlightOpacity);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        Assert.Null(failure);
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

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var frame = new DispatcherFrame();
        var deadline = DateTimeOffset.UtcNow + timeout;
        var timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        timer.Tick += (_, _) =>
        {
            if (condition() || DateTimeOffset.UtcNow >= deadline)
            {
                timer.Stop();
                frame.Continue = false;
            }
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        Assert.True(condition(), $"The WPF animation did not finish within {timeout.TotalMilliseconds:0} ms.");
    }
}
