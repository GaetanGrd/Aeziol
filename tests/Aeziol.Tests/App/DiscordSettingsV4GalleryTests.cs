using System.Xml.Linq;
using Aeziol.App.DiscordSettingsV4;

namespace Aeziol.Tests.App;

[Collection(WpfUiTestGroup.Name)]
public sealed class DiscordSettingsV4GalleryTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void GalleryLoadsAndOffersFourProposals()
    {
        WpfTestHost.Run(() => _ = new DiscordSettingsV4Gallery());

        var path = FindSourceFile("src", "Aeziol.App", "DiscordSettingsV4", "DiscordSettingsV4Gallery.xaml");
        var document = XDocument.Load(path);
        var selectors = document.Descendants()
            .Where(element => element.Name.LocalName == "RadioButton"
                && element.Attribute("GroupName")?.Value == "DiscordSettingsV4Gallery")
            .ToArray();
        var concepts = document.Descendants()
            .Where(element => element.Name.LocalName.StartsWith("DiscordSettingsV4Concept", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(4, selectors.Length);
        Assert.Equal(["1", "2", "3", "4"], selectors.Select(element => element.Attribute("Content")?.Value));
        Assert.Equal(4, concepts.Length);
    }

    [Fact]
    public void EveryProposalContainsOnlyTheApprovedSettingTags()
    {
        var expected = new[] { "2", "3", "7", "8", "9" };
        var namedTags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DiscordSettingsV4:RestoreDelay"] = "2",
            ["DiscordSettingsV4:ProtectedOutputs"] = "3",
            ["DiscordSettingsV4:RevokeAuthorization"] = "7",
            ["DiscordSettingsV4:ManualDiscordExecutable"] = "8",
            ["DiscordSettingsV4:WindowsNotification"] = "9",
        };
        for (var concept = 1; concept <= 4; concept++)
        {
            var path = FindSourceFile(
                "src", "Aeziol.App", "DiscordSettingsV4", $"DiscordSettingsV4Concept{concept}.xaml");
            var document = XDocument.Load(path);
            var tags = document.Descendants()
                .Select(element => element.Attribute("Tag")?.Value)
                .Where(value => value is not null)
                .Cast<string>()
                .Select(value => value.StartsWith("DiscordSetting:", StringComparison.Ordinal)
                    ? value["DiscordSetting:".Length..]
                    : namedTags.GetValueOrDefault(value, value))
                .Order(StringComparer.Ordinal)
                .ToArray();

            Assert.Equal(expected, tags);
        }
    }

    [Fact]
    public void MainWindowKeepsRoutingAndReplacesOnlyTheLegacyRulesContent()
    {
        var xamlPath = FindSourceFile("src", "Aeziol.App", "MainWindow.xaml");
        var codePath = FindSourceFile("src", "Aeziol.App", "MainWindow.xaml.cs");
        var document = XDocument.Load(xamlPath);
        var code = File.ReadAllText(codePath);

        var routing = FindNamedElement(document, "PassageAutomationContent");
        var legacyRules = FindNamedElement(document, "LegacyDiscordRulesLayout");
        var settingsHost = FindNamedElement(document, "DiscordSettingsHost");

        Assert.Null(routing.Attribute("Visibility"));
        Assert.Equal("Collapsed", legacyRules.Attribute("Visibility")?.Value);
        Assert.NotEqual("Collapsed", settingsHost.Attribute("Visibility")?.Value);
        Assert.Contains("DiscordSettingsHost.Content = new DiscordSettingsV4.DiscordSettingsV4Gallery();", code, StringComparison.Ordinal);
        Assert.DoesNotContain("DiscordSettingsHost.Content = DiscordSettingsCard;", code, StringComparison.Ordinal);
    }

    private static XElement FindNamedElement(XDocument document, string name) =>
        document.Descendants().Single(element => element.Attribute(Xaml + "Name")?.Value == name);

    private static string FindSourceFile(params string[] relativeSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeSegments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativeSegments)} from the test output directory.");
    }
}
