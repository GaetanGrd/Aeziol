using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Aeziol.App.Appearance;
using Aeziol.App.Localization;
using Aeziol.App.Settings;
using MediaColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfRadioButton = System.Windows.Controls.RadioButton;
using WpfScrollBar = System.Windows.Controls.Primitives.ScrollBar;
using WpfSize = System.Windows.Size;
using ShapePath = System.Windows.Shapes.Path;

namespace Aeziol.Tests.App;

[Collection(WpfUiTestGroup.Name)]
public sealed class ButtonStyleTests
{
    private static readonly string[] SemanticButtonStyleKeys =
    [
        "PrimaryButton",
        "QuietButton",
        "ActionButton",
        "DangerButton",
        "SuccessButton",
        "WarningButton",
    ];

    [Fact]
    public void ChaosStains_AreAsymmetricCompoundMassesRatherThanSimpleBandsOrDiscs()
    {
        var brush = CorruptionBrushFactory.CreateStainedSurface(
            new Random(2718),
            Colors.Black,
            Colors.DarkViolet,
            Colors.Crimson);

        var drawing = Assert.IsType<DrawingGroup>(brush.Drawing);
        Assert.True(drawing.Children.Count > 2);
        foreach (var stain in drawing.Children.Skip(1).Cast<GeometryDrawing>())
        {
            var mass = Assert.IsType<GeometryGroup>(stain.Geometry);
            Assert.InRange(mass.Children.Count, 3, 6);
            Assert.All(mass.Children, lobe => Assert.IsType<StreamGeometry>(lobe));
            Assert.IsType<RotateTransform>(mass.Transform);
        }
    }

    [Fact]
    public void InteractiveControls_UseSecondaryPressFeedbackAndKeepContentCentered()
    {
        Exception? failure = null;
        WpfTestHost.Run(() =>
        {
            try
            {
                AeziolThemeService.Apply(AeziolTheme.Elgo);
                var application = Assert.IsType<Aeziol.App.App>(System.Windows.Application.Current);
                var secondary = Assert.IsType<SolidColorBrush>(application.Resources["AeziolSecondary"]).Color;
                var onSecondary = Assert.IsType<SolidColorBrush>(application.Resources["AeziolOnSecondary"]).Color;

                var primaryButton = new PressableButton
                {
                    Width = 180,
                    Height = 44,
                    Content = "Authorize Discord",
                    Style = Assert.IsType<Style>(application.Resources["PrimaryButton"]),
                };
                var windowGlyph = new ShapePath
                {
                    Width = 12,
                    Height = 12,
                    Data = Geometry.Parse("M 1,1 L 13,1 L 13,13 L 1,13 Z"),
                };
                var windowButton = new PressableButton
                {
                    Content = windowGlyph,
                    Style = Assert.IsType<Style>(application.Resources["WindowButton"]),
                };
                var settingsCommand = new PressableButton
                {
                    Width = 260,
                    Content = "Language",
                    Style = Assert.IsType<Style>(application.Resources["SettingsCommandRow"]),
                };
                var navContent = new StackPanel
                {
                    Width = 28,
                    Children =
                    {
                        new ShapePath
                        {
                            Width = 21,
                            Height = 21,
                            Data = Assert.IsType<GeometryGroup>(application.Resources["SettingsRingsGeometry"]),
                        },
                        new TextBlock
                        {
                            Text = "Settings",
                            FontSize = 9,
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        },
                    },
                };
                var navButton = new PressableRadioButton
                {
                    Content = navContent,
                    Style = Assert.IsType<Style>(application.Resources["NavRadio"]),
                };
                var settingsTab = new PressableRadioButton
                {
                    Content = "General",
                    Style = Assert.IsType<Style>(application.Resources["SettingsSectionTab"]),
                };

                var host = new StackPanel { Width = 280 };
                host.Children.Add(primaryButton);
                host.Children.Add(windowButton);
                host.Children.Add(settingsCommand);
                host.Children.Add(navButton);
                host.Children.Add(settingsTab);
                primaryButton.ApplyTemplate();
                windowButton.ApplyTemplate();
                settingsCommand.ApplyTemplate();
                navButton.ApplyTemplate();
                settingsTab.ApplyTemplate();
                host.Measure(new WpfSize(280, 320));
                host.Arrange(new Rect(0, 0, 280, 320));
                host.UpdateLayout();

                var primaryLabel = Assert.IsType<TextBlock>(GetTemplateBorder(primaryButton, "Chrome").Child);
                AssertCentered(primaryButton, primaryLabel);
                AssertCentered(windowButton, windowGlyph);
                AssertCentered(navButton, navContent);
                Assert.Equal(VerticalAlignment.Center, FindVisualChild<ContentPresenter>(settingsTab).VerticalAlignment);
                Assert.Equal(VerticalAlignment.Center, FindVisualChild<ContentPresenter>(settingsCommand).VerticalAlignment);

                primaryButton.SetPressed(true);
                windowButton.SetPressed(true);
                settingsCommand.SetPressed(true);
                navButton.SetPressed(true);
                settingsTab.SetPressed(true);
                host.UpdateLayout();

                Assert.Equal(secondary, GetBackgroundColor(GetTemplateBorder(primaryButton, "Chrome")));
                Assert.Equal(secondary, GetBackgroundColor(GetTemplateBorder(windowButton, "Chrome")));
                Assert.Equal(secondary, GetBackgroundColor(GetTemplateBorder(settingsCommand, "Surface")));
                Assert.Equal(secondary, GetBackgroundColor(GetTemplateBorder(navButton, "Selection")));
                Assert.Equal(secondary, GetBackgroundColor(GetTemplateBorder(settingsTab, "HoverSurface")));
                Assert.Equal(onSecondary, Assert.IsType<SolidColorBrush>(primaryButton.Foreground).Color);
                Assert.Equal(onSecondary, Assert.IsType<SolidColorBrush>(windowButton.Foreground).Color);
                Assert.Equal(onSecondary, Assert.IsType<SolidColorBrush>(settingsCommand.Foreground).Color);
                Assert.Equal(onSecondary, Assert.IsType<SolidColorBrush>(navButton.Foreground).Color);
                Assert.Equal(onSecondary, Assert.IsType<SolidColorBrush>(settingsTab.Foreground).Color);

            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        Assert.Null(failure);
    }

    [Fact]
    public void ButtonTemplates_UseEnabledSecondaryHoverAndPreserveSemanticNormalStates()
    {
        Exception? failure = null;
        WpfTestHost.Run(() =>
        {
            try
            {
                AeziolThemeService.Apply(AeziolTheme.Elgo);
                var application = Assert.IsType<Aeziol.App.App>(System.Windows.Application.Current);
                var secondary = Assert.IsType<SolidColorBrush>(application.Resources["AeziolSecondary"]).Color;
                var onSecondary = Assert.IsType<SolidColorBrush>(application.Resources["AeziolOnSecondary"]).Color;
                var semanticButtons = SemanticButtonStyleKeys.Select(key => new PressableButton
                {
                    Width = 180,
                    Height = 44,
                    Content = key,
                    Style = Assert.IsType<Style>(application.Resources[key]),
                }).ToArray();
                var windowButton = new PressableButton
                {
                    Content = "?",
                    Style = Assert.IsType<Style>(application.Resources["WindowButton"]),
                };
                var settingsCommand = new PressableButton
                {
                    Width = 260,
                    Content = "Language",
                    Style = Assert.IsType<Style>(application.Resources["SettingsCommandRow"]),
                };
                var allButtons = semanticButtons.Append(windowButton).Append(settingsCommand).ToArray();
                var initialStates = allButtons.Select(button => new
                {
                    Background = button.Background,
                    Foreground = button.Foreground,
                    BorderBrush = button.BorderBrush,
                }).ToArray();

                var host = new StackPanel { Width = 280 };
                foreach (var button in allButtons)
                {
                    host.Children.Add(button);
                    button.ApplyTemplate();
                }

                host.Measure(new WpfSize(280, 500));
                host.Arrange(new Rect(0, 0, 280, 500));
                host.UpdateLayout();

                AssertEnabledSecondaryHover(semanticButtons[0].Template, "Chrome");
                AssertEnabledSecondaryHover(windowButton.Template, "Chrome");
                AssertEnabledSecondaryHover(settingsCommand.Template, "Surface");

                Assert.Equal(System.Windows.HorizontalAlignment.Center, semanticButtons[0].HorizontalContentAlignment);
                Assert.Equal(VerticalAlignment.Center, semanticButtons[0].VerticalContentAlignment);
                Assert.Equal(System.Windows.HorizontalAlignment.Center, windowButton.HorizontalContentAlignment);
                Assert.Equal(VerticalAlignment.Center, windowButton.VerticalContentAlignment);
                Assert.Equal(VerticalAlignment.Center, settingsCommand.VerticalContentAlignment);

                foreach (var button in allButtons)
                {
                    button.SetPressed(true);
                }

                host.UpdateLayout();
                foreach (var button in semanticButtons)
                {
                    Assert.Equal(secondary, GetBackgroundColor(GetTemplateBorder(button, "Chrome")));
                    Assert.Equal(onSecondary, Assert.IsType<SolidColorBrush>(button.Foreground).Color);
                }

                Assert.Equal(secondary, GetBackgroundColor(GetTemplateBorder(windowButton, "Chrome")));
                Assert.Equal(onSecondary, Assert.IsType<SolidColorBrush>(windowButton.Foreground).Color);
                Assert.Equal(secondary, GetBackgroundColor(GetTemplateBorder(settingsCommand, "Surface")));
                Assert.Equal(onSecondary, Assert.IsType<SolidColorBrush>(settingsCommand.Foreground).Color);

                foreach (var button in allButtons)
                {
                    button.SetPressed(false);
                }

                host.UpdateLayout();
                for (var index = 0; index < allButtons.Length; index++)
                {
                    Assert.Equal(initialStates[index].Background, allButtons[index].Background);
                    Assert.Equal(initialStates[index].Foreground, allButtons[index].Foreground);
                    Assert.Equal(initialStates[index].BorderBrush, allButtons[index].BorderBrush);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        Assert.Null(failure);
    }

    [Fact]
    public void CompactOutputSelector_KeepsItsArrowNearShortNamesAndCapsLongNames()
    {
        Exception? failure = null;
        WpfTestHost.Run(() =>
        {
            try
            {
                var application = Assert.IsType<Aeziol.App.App>(System.Windows.Application.Current);
                var style = Assert.IsType<Style>(application.Resources["CompactComboBox"]);
                var shortName = new WpfComboBox
                {
                    MaxWidth = 240,
                    Style = style,
                };
                shortName.Items.Add("Speakers");
                shortName.SelectedIndex = 0;
                var longName = new WpfComboBox
                {
                    MaxWidth = 180,
                    Style = style,
                };
                longName.Items.Add("A very long Windows audio endpoint name that needs trimming");
                longName.SelectedIndex = 0;

                var host = new StackPanel { Width = 280 };
                host.Children.Add(shortName);
                host.Children.Add(longName);
                host.Measure(new WpfSize(280, 140));
                host.Arrange(new Rect(0, 0, 280, 140));
                shortName.ApplyTemplate();
                longName.ApplyTemplate();
                host.UpdateLayout();

                var shortToggle = FindVisualChild<ToggleButton>(shortName);
                shortToggle.ApplyTemplate();
                var shortArrow = FindVisualChild<ShapePath>(shortToggle);
                var arrowCenter = shortArrow.TranslatePoint(
                    new System.Windows.Point(shortArrow.ActualWidth / 2, shortArrow.ActualHeight / 2),
                    shortName);
                Assert.Equal(System.Windows.HorizontalAlignment.Left, shortName.HorizontalAlignment);
                Assert.InRange(shortName.ActualWidth, 60, 150);
                Assert.InRange(arrowCenter.X, 35, 145);
                Assert.InRange(longName.ActualWidth, 100, 180);

            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        Assert.Null(failure);
    }

    [Fact]
    public void SettingsIcon_UsesTwoClosedConcentricRings()
    {
        Exception? failure = null;
        WpfTestHost.Run(() =>
        {
            try
            {
                var application = Assert.IsType<Aeziol.App.App>(System.Windows.Application.Current);
                var rings = Assert.IsType<GeometryGroup>(application.Resources["SettingsRingsGeometry"]);
                var ellipses = rings.Children.Cast<EllipseGeometry>().ToArray();

                Assert.Equal(2, ellipses.Length);
                Assert.Equal(ellipses[0].Center, ellipses[1].Center);
                Assert.True(ellipses[0].RadiusX > ellipses[1].RadiusX);
                Assert.All(ellipses, ring => Assert.Equal(ring.RadiusX, ring.RadiusY));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        Assert.Null(failure);
    }

    [Fact]
    public void ThemedControls_RenderTheirCriticalVisualProperties()
    {
        MediaColor? renderedColor = null;
        Exception? failure = null;
        WpfTestHost.Run(() =>
        {
            try
            {
                var application = Assert.IsType<Aeziol.App.App>(System.Windows.Application.Current);
                var button = new WpfButton
                {
                    Content = "Autoriser Discord",
                    Style = Assert.IsType<Style>(application.Resources["PrimaryButton"]),
                };

                button.Measure(new WpfSize(220, 60));
                button.Arrange(new Rect(button.DesiredSize));
                button.ApplyTemplate();

                var label = FindVisualChild<TextBlock>(button);
                renderedColor = Assert.IsType<SolidColorBrush>(label.Foreground).Color;

                var comboBox = new WpfComboBox
                {
                    Style = Assert.IsType<Style>(application.Resources[typeof(WpfComboBox)]),
                };
                comboBox.Measure(new WpfSize(240, 48));
                comboBox.Arrange(new Rect(comboBox.DesiredSize));
                comboBox.ApplyTemplate();
                var popup = Assert.IsType<Popup>(comboBox.Template.FindName("PART_Popup", comboBox));
                Assert.True(popup.AllowsTransparency);
                var arrow = FindVisualChild<ShapePath>(comboBox);
                Assert.Equal(12, arrow.Width);
                Assert.Equal(12, arrow.Height);
                Assert.Equal(System.Windows.HorizontalAlignment.Center, arrow.HorizontalAlignment);
                Assert.Equal(VerticalAlignment.Center, arrow.VerticalAlignment);

                var toggle = new WpfCheckBox
                {
                    Style = Assert.IsType<Style>(application.Resources["ToggleSwitch"]),
                };
                var maximizeGlyph = new ShapePath
                {
                    Width = 12,
                    Height = 12,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Data = Geometry.Parse("M 1,1 L 13,1 L 13,13 L 1,13 Z"),
                };
                var windowButton = new WpfButton
                {
                    Style = Assert.IsType<Style>(application.Resources["WindowButton"]),
                    Content = maximizeGlyph,
                };
                var actionButton = new WpfButton
                {
                    Style = Assert.IsType<Style>(application.Resources["ActionButton"]),
                    Content = "Browse",
                };
                var resetButton = new WpfButton
                {
                    Style = Assert.IsType<Style>(application.Resources["SettingResetButton"]),
                };
                var dangerButton = new WpfButton
                {
                    Style = Assert.IsType<Style>(application.Resources["DangerButton"]),
                    Content = "Reset application settings",
                };
                var successButton = new WpfButton
                {
                    Style = Assert.IsType<Style>(application.Resources["SuccessButton"]),
                    Content = "Enable",
                };
                var settingsTab = new WpfRadioButton
                {
                    Style = Assert.IsType<Style>(application.Resources["SettingsSectionTab"]),
                    Content = "General",
                    IsChecked = true,
                };
                var settingsCommand = new WpfButton
                {
                    Style = Assert.IsType<Style>(application.Resources["SettingsCommandRow"]),
                    Content = "Language",
                };
                var settingsPath = new ContentControl
                {
                    Style = Assert.IsType<Style>(application.Resources["SettingsPathRow"]),
                    Content = new TextBlock { Text = "Reduce animations" },
                };
                var hoverResetButton = new WpfButton
                {
                    Style = Assert.IsType<Style>(application.Resources["SettingsHoverResetButton"]),
                };
                var scrollBar = new WpfScrollBar
                {
                    Style = Assert.IsType<Style>(application.Resources[typeof(WpfScrollBar)]),
                };
                var worldLogo = new System.Windows.Shapes.Rectangle
                {
                    Style = Assert.IsType<Style>(application.Resources["AboutWorldLogo"]),
                };
                var musicArtwork = new Grid
                {
                    Style = Assert.IsType<Style>(application.Resources["MusicArtwork"]),
                };
                var volumeSlider = new Slider
                {
                    Style = Assert.IsType<Style>(application.Resources["AmbientVolumeSlider"]),
                    Value = 8,
                };
                var journeyBorder = new Border();
                journeyBorder.SetResourceReference(Border.BorderBrushProperty, "AeziolTraceBorder");
                var journeyParticle = new System.Windows.Shapes.Ellipse();
                journeyParticle.SetResourceReference(
                    System.Windows.Shapes.Shape.FillProperty,
                    "AeziolJourneyParticlePrimary");
                var deviceCheck = new System.Windows.Controls.CheckBox
                {
                    Style = Assert.IsType<Style>(application.Resources["DeviceCheck"]),
                    Content = "Device",
                };
                var motionHost = new StackPanel();
                motionHost.Children.Add(toggle);
                motionHost.Children.Add(deviceCheck);
                motionHost.Children.Add(windowButton);
                motionHost.Children.Add(actionButton);
                motionHost.Children.Add(resetButton);
                motionHost.Children.Add(dangerButton);
                motionHost.Children.Add(successButton);
                motionHost.Children.Add(settingsTab);
                motionHost.Children.Add(settingsCommand);
                motionHost.Children.Add(settingsPath);
                motionHost.Children.Add(hoverResetButton);
                motionHost.Children.Add(scrollBar);
                motionHost.Children.Add(worldLogo);
                motionHost.Children.Add(musicArtwork);
                motionHost.Children.Add(volumeSlider);
                motionHost.Children.Add(journeyBorder);
                motionHost.Children.Add(journeyParticle);
                var testWindow = new Window
                {
                    Width = 220,
                    Height = 100,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    Opacity = 0,
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = -32000,
                    Top = -32000,
                    WindowStyle = WindowStyle.None,
                    Content = motionHost,
                };
                testWindow.Show();
                toggle.ApplyTemplate();
                var thumb = FindVisualChild<System.Windows.Shapes.Ellipse>(toggle);
                Assert.Equal(3, thumb.Margin.Left);

                toggle.IsChecked = true;
                toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                WaitUntil(() => thumb.Margin.Left >= 22.5, TimeSpan.FromSeconds(1));
                Assert.InRange(thumb.Margin.Left, 22.5, 23.5);

                windowButton.ApplyTemplate();
                var iconCenter = maximizeGlyph.TranslatePoint(
                    new System.Windows.Point(maximizeGlyph.ActualWidth / 2, maximizeGlyph.ActualHeight / 2),
                    windowButton);
                Assert.InRange(iconCenter.X, (windowButton.ActualWidth / 2) - 0.5, (windowButton.ActualWidth / 2) + 0.5);
                Assert.InRange(iconCenter.Y, (windowButton.ActualHeight / 2) - 0.5, (windowButton.ActualHeight / 2) + 0.5);
                Assert.True(HasVisibleColor(actionButton.Background));
                Assert.True(actionButton.BorderThickness.Left > 0);
                Assert.Equal(25, resetButton.Width);
                Assert.Equal(25, resetButton.Height);
                Assert.Equal("↺", resetButton.Content);
                Assert.Equal(
                    Assert.IsType<SolidColorBrush>(application.Resources["AeziolDanger"]).Color,
                    Assert.IsType<SolidColorBrush>(dangerButton.Foreground).Color);
                Assert.Equal(
                    Assert.IsType<SolidColorBrush>(application.Resources["AeziolSuccess"]).Color,
                    Assert.IsType<SolidColorBrush>(successButton.Background).Color);
                Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(successButton.Foreground).Color);
                settingsTab.ApplyTemplate();
                Assert.Equal(38, settingsTab.Height);
                Assert.NotNull(settingsTab.Template);
                settingsCommand.ApplyTemplate();
                settingsPath.ApplyTemplate();
                Assert.Equal(42, settingsCommand.Height);
                Assert.Equal(42, settingsPath.Height);
                Assert.NotNull(settingsCommand.Template);
                Assert.NotNull(settingsPath.Template);
                Assert.Equal(0.28, hoverResetButton.Opacity, 2);
                Assert.Equal(10, scrollBar.Width);
                Assert.NotNull(scrollBar.Template);
                Assert.Equal(worldLogo.Width, worldLogo.Height);
                Assert.Equal(10, worldLogo.RadiusX);
                Assert.Equal(10, worldLogo.RadiusY);
                Assert.Equal(musicArtwork.Width, musicArtwork.Height);
                var artworkClip = Assert.IsType<RectangleGeometry>(musicArtwork.Clip);
                Assert.Equal(new Rect(0, 0, 52, 52), artworkClip.Rect);
                Assert.Equal(6, artworkClip.RadiusX);
                Assert.Equal(6, artworkClip.RadiusY);
                Assert.Equal(52, artworkClip.Rect.Width);
                Assert.Equal(52, artworkClip.Rect.Height);
                Assert.Equal(0, volumeSlider.Minimum);
                Assert.Equal(100, volumeSlider.Maximum);
                Assert.True(volumeSlider.IsMoveToPointEnabled);
                volumeSlider.ApplyTemplate();
                var volumeThumb = FindVisualChild<Thumb>(volumeSlider);
                Assert.Equal(16, volumeThumb.Width);
                Assert.Equal(16, volumeThumb.Height);

                AeziolThemeService.Apply(AeziolTheme.Cherry);
                var cherryBorder = Assert.IsType<LinearGradientBrush>(journeyBorder.BorderBrush);
                var cherryParticle = Assert.IsType<RadialGradientBrush>(journeyParticle.Fill);
                Assert.Equal(
                    AeziolThemeService.GetPalette(AeziolTheme.Cherry).Primary.R,
                    cherryBorder.GradientStops[1].Color.R);
                Assert.Equal(
                    AeziolThemeService.GetPalette(AeziolTheme.Cherry).Primary,
                    cherryParticle.GradientStops[0].Color);

                AeziolThemeService.Apply(AeziolTheme.Yuna);
                var yunaBorder = Assert.IsType<LinearGradientBrush>(journeyBorder.BorderBrush);
                var yunaParticle = Assert.IsType<RadialGradientBrush>(journeyParticle.Fill);
                Assert.NotEqual(cherryBorder.GradientStops[1].Color, yunaBorder.GradientStops[1].Color);
                Assert.NotEqual(cherryParticle.GradientStops[0].Color, yunaParticle.GradientStops[0].Color);
                Assert.Equal(
                    AeziolThemeService.GetPalette(AeziolTheme.Yuna).Primary,
                    yunaParticle.GradientStops[0].Color);

                AeziolThemeService.Apply(AeziolTheme.Chaos);
                var chaosBorder = Assert.IsType<DrawingBrush>(journeyBorder.BorderBrush);
                var chaosParticle = Assert.IsType<RadialGradientBrush>(journeyParticle.Fill);
                Assert.NotNull(chaosBorder.Drawing);
                Assert.True(chaosParticle.GradientStops.Count > yunaParticle.GradientStops.Count);
                var chaosLogo = Assert.IsType<DrawingImage>(application.Resources["AeziolCicadaDrawing"]);
                var chaosLogoGroup = Assert.IsType<DrawingGroup>(chaosLogo.Drawing);
                var chaosWing = Assert.IsType<GeometryDrawing>(chaosLogoGroup.Children[0]);
                Assert.IsType<SolidColorBrush>(chaosWing.Pen?.Brush);
                Assert.NotNull(chaosWing.Geometry?.Transform);
                Assert.IsType<DrawingBrush>(chaosLogoGroup.OpacityMask);
                Assert.True(chaosLogoGroup.Children.Count > 8);
                Assert.IsType<DrawingBrush>(application.Resources["AeziolCanvas"]);
                Assert.IsType<DrawingBrush>(application.Resources["AeziolBorder"]);

                deviceCheck.ApplyTemplate();
                deviceCheck.IsChecked = true;
                var tick = Assert.IsType<System.Windows.Shapes.Path>(deviceCheck.Template.FindName("Tick", deviceCheck));
                Assert.True(Assert.IsType<ScaleTransform>(tick.RenderTransform).IsFrozen);
                deviceCheck.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.False(Assert.IsType<ScaleTransform>(tick.RenderTransform).IsFrozen);
                WaitUntil(
                    () => Assert.IsType<ScaleTransform>(tick.RenderTransform).ScaleX >= 0.995,
                    TimeSpan.FromSeconds(1));
                Assert.Equal(1, Assert.IsType<ScaleTransform>(tick.RenderTransform).ScaleX, 2);

                AeziolThemeService.Apply(AeziolTheme.Elgo);
                var restoredLogo = Assert.IsType<DrawingImage>(application.Resources["AeziolCicadaDrawing"]);
                var restoredLogoGroup = Assert.IsType<DrawingGroup>(restoredLogo.Drawing);
                var restoredWing = Assert.IsType<GeometryDrawing>(restoredLogoGroup.Children[0]);
                Assert.IsType<SolidColorBrush>(restoredWing.Pen?.Brush);
                Assert.True(restoredWing.Geometry?.Transform?.Value.IsIdentity ?? true);
                Assert.Null(restoredLogoGroup.OpacityMask);
                Assert.Equal(8, restoredLogoGroup.Children.Count);
                Assert.IsType<SolidColorBrush>(application.Resources["AeziolCanvas"]);
                Assert.IsType<SolidColorBrush>(application.Resources["AeziolBorder"]);

                toggle.IsChecked = false;
                toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                WaitUntil(() => thumb.Margin.Left <= 3.5, TimeSpan.FromSeconds(1));
                MotionAssist.SetIsReduced(motionHost, true);
                Assert.True(MotionAssist.GetIsReduced(toggle));
                toggle.IsChecked = true;
                toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                toggle.UpdateLayout();
                Assert.Equal(23, thumb.Margin.Left);

                MotionAssist.SetIsReduced(motionHost, false);
                Assert.Equal(23, thumb.Margin.Left);
                PumpDispatcher(TimeSpan.FromMilliseconds(240));
                Assert.Equal(23, thumb.Margin.Left);

                var localization = new LocalizationService(
                    Path.Combine(AppContext.BaseDirectory, "Localization"),
                    Path.Combine(Path.GetTempPath(), "Aeziol.Tests", "Languages"),
                    "en");
                var firstRun = new Aeziol.App.FirstRunWindow(
                    localization,
                    "en",
                    Aeziol.App.Settings.AeziolTheme.Elgo,
                    enhanceContrast: false);
                Assert.Equal(WindowStyle.None, firstRun.WindowStyle);
                Assert.NotNull(WindowChrome.GetWindowChrome(firstRun));
                Assert.Equal("en", firstRun.SelectedLanguage);
                Assert.Equal(Aeziol.App.Settings.AeziolTheme.Elgo, firstRun.SelectedTheme);
                firstRun.Close();
                testWindow.Close();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        Assert.Null(failure);
        Assert.Equal(Colors.Black, renderedColor);
    }

    private static void WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var frame = new DispatcherFrame();
        var stopwatch = Stopwatch.StartNew();
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.CurrentDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(16),
        };
        timer.Tick += (_, _) =>
        {
            if (condition() || stopwatch.Elapsed >= timeout)
            {
                timer.Stop();
                frame.Continue = false;
            }
        };
        timer.Start();
        Dispatcher.PushFrame(frame);
        Assert.True(condition(), $"The WPF animation did not finish within {timeout.TotalMilliseconds:0} ms.");
    }

    private static void PumpDispatcher(TimeSpan duration)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher.CurrentDispatcher)
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

    private static bool HasVisibleColor(System.Windows.Media.Brush brush) => brush switch
    {
        SolidColorBrush solid => solid.Color.A > 0,
        GradientBrush gradient => gradient.GradientStops.Any(stop => stop.Color.A > 0),
        _ => brush.Opacity > 0,
    };

    private static void AssertEnabledSecondaryHover(ControlTemplate template, string backgroundTarget)
    {
        var hoverTrigger = Assert.Single(template.Triggers.OfType<MultiTrigger>(), trigger =>
            HasCondition(trigger, UIElement.IsMouseOverProperty, true) &&
            HasCondition(trigger, UIElement.IsEnabledProperty, true));

        AssertDynamicResourceSetter(
            hoverTrigger.Setters,
            Border.BackgroundProperty,
            backgroundTarget,
            "AeziolSecondary");
        AssertDynamicResourceSetter(
            hoverTrigger.Setters,
            System.Windows.Controls.Control.ForegroundProperty,
            targetName: null,
            "AeziolOnSecondary");

        var pressedTrigger = Assert.Single(template.Triggers.OfType<Trigger>(), trigger =>
            trigger.Property == System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty && Equals(trigger.Value, true));
        AssertDynamicResourceSetter(
            pressedTrigger.Setters,
            Border.BackgroundProperty,
            backgroundTarget,
            "AeziolSecondary");
        AssertDynamicResourceSetter(
            pressedTrigger.Setters,
            System.Windows.Controls.Control.ForegroundProperty,
            targetName: null,
            "AeziolOnSecondary");

        Assert.Single(template.Triggers.OfType<Trigger>(), trigger =>
            trigger.Property == UIElement.IsEnabledProperty && Equals(trigger.Value, false));
        Assert.Single(template.Triggers.OfType<Trigger>(), trigger =>
            trigger.Property == UIElement.IsKeyboardFocusedProperty && Equals(trigger.Value, true));
    }

    private static bool HasCondition(MultiTrigger trigger, DependencyProperty property, object value) =>
        trigger.Conditions.Cast<Condition>().Any(condition =>
            condition.Property == property && Equals(condition.Value, value));

    private static void AssertDynamicResourceSetter(
        SetterBaseCollection setters,
        DependencyProperty property,
        string? targetName,
        object resourceKey)
    {
        var setter = Assert.Single(setters.OfType<Setter>(), candidate =>
            candidate.Property == property && candidate.TargetName == targetName);
        var resource = Assert.IsType<DynamicResourceExtension>(setter.Value);
        Assert.Equal(resourceKey, resource.ResourceKey);
    }

    private static Border GetTemplateBorder(System.Windows.Controls.Control control, string name)
    {
        control.ApplyTemplate();
        return Assert.IsType<Border>(control.Template.FindName(name, control));
    }

    private static MediaColor GetBackgroundColor(Border border) =>
        Assert.IsType<SolidColorBrush>(border.Background).Color;

    private static void AssertCentered(FrameworkElement parent, FrameworkElement content)
    {
        var center = content.TranslatePoint(
            new System.Windows.Point(content.ActualWidth / 2, content.ActualHeight / 2),
            parent);
        Assert.InRange(center.X, (parent.ActualWidth / 2) - 0.5, (parent.ActualWidth / 2) + 0.5);
        Assert.InRange(center.Y, (parent.ActualHeight / 2) - 0.5, (parent.ActualHeight / 2) + 0.5);
    }

    private static T FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            try
            {
                return FindVisualChild<T>(child);
            }
            catch (InvalidOperationException)
            {
            }
        }

        throw new InvalidOperationException($"No visual child of type {typeof(T).Name} was found.");
    }

    private sealed class PressableButton : WpfButton
    {
        public void SetPressed(bool value) => IsPressed = value;
    }

    private sealed class PressableRadioButton : WpfRadioButton
    {
        public void SetPressed(bool value) => IsPressed = value;
    }
}
