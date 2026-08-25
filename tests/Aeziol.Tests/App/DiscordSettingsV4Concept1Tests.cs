using System.Xml.Linq;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;
using WpfComboBox = System.Windows.Controls.ComboBox;

namespace Aeziol.Tests.App;

[Collection(WpfUiTestGroup.Name)]
public sealed class DiscordSettingsV4Concept1Tests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void PrototypeContainsExactlyTheFiveAuthorizedSettings()
    {
        var document = LoadPrototype();
        var markedSettings = document.Descendants()
            .Where(element => element.Attribute("Tag")?.Value.StartsWith(
                "DiscordSettingsV4:",
                StringComparison.Ordinal) == true)
            .ToArray();
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DiscordSettingsV4:RestoreDelay"] = "RestoreDelaySetting",
            ["DiscordSettingsV4:ProtectedOutputs"] = "ProtectedOutputsSetting",
            ["DiscordSettingsV4:WindowsNotification"] = "WindowsNotificationSetting",
            ["DiscordSettingsV4:ManualDiscordExecutable"] = "ManualDiscordExecutableSetting",
            ["DiscordSettingsV4:RevokeAuthorization"] = "RevokeDiscordAuthorizationSetting",
        };

        Assert.Equal(5, markedSettings.Length);
        Assert.All(markedSettings, setting => Assert.Equal("Border", setting.Name.LocalName));
        Assert.Equal(
            expected.Keys.Order(StringComparer.Ordinal),
            markedSettings.Select(setting => setting.Attribute("Tag")!.Value).Order(StringComparer.Ordinal));

        foreach (var setting in markedSettings)
        {
            var tag = setting.Attribute("Tag")!.Value;
            Assert.Equal(expected[tag], setting.Attribute(Xaml + "Name")?.Value);
        }

        Assert.Contains("Délai avant restauration de la sortie", FlattenText(markedSettings.Single(
            setting => setting.Attribute("Tag")?.Value == "DiscordSettingsV4:RestoreDelay")), StringComparison.Ordinal);
        Assert.Contains("Sorties à ne jamais modifier", FlattenText(markedSettings.Single(
            setting => setting.Attribute("Tag")?.Value == "DiscordSettingsV4:ProtectedOutputs")), StringComparison.Ordinal);
        Assert.Contains("Envoyer une notification Windows", FlattenText(markedSettings.Single(
            setting => setting.Attribute("Tag")?.Value == "DiscordSettingsV4:WindowsNotification")), StringComparison.Ordinal);
        Assert.Contains("Configurer manuellement Discord.exe", FlattenText(markedSettings.Single(
            setting => setting.Attribute("Tag")?.Value == "DiscordSettingsV4:ManualDiscordExecutable")), StringComparison.Ordinal);
        Assert.Contains("Révoquer l’autorisation Discord", FlattenText(markedSettings.Single(
            setting => setting.Attribute("Tag")?.Value == "DiscordSettingsV4:RevokeAuthorization")), StringComparison.Ordinal);
    }

    [Fact]
    public void PrototypeDoesNotDuplicateMainDiscordControlsOrUseForbiddenStaging()
    {
        var document = LoadPrototype();
        var source = document.ToString(SaveOptions.DisableFormatting);
        var forbiddenNames = new[]
        {
            "DiscordStatus",
            "DiscordConnection",
            "AuthorizeDiscord",
            "AutomationEnabled",
            "ModuleEnabled",
            "Trigger",
            "Condition",
            "Priority",
            "TargetOutput",
            "CurrentOutput",
            "ForceRestore",
            "DiscordToAeziol",
            "JourneyTrace",
        };
        var forbiddenCopy = new[]
        {
            "état Discord",
            "connexion Discord",
            "autoriser Discord",
            "activer le module",
            "déclencheur",
            "condition",
            "priorité",
            "sortie actuelle",
            "restaurer maintenant",
            "restauration forcée",
            "Discord vers Aeziol",
            "Discord -> Aeziol",
            "Discord → Aeziol",
            "score",
            "orbite",
            "prédiction",
            "diagnostic intelligent",
        };

        Assert.DoesNotContain(document.Descendants(), element =>
        {
            var name = element.Attribute(Xaml + "Name")?.Value;
            return name is not null && forbiddenNames.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase));
        });
        Assert.All(forbiddenCopy, phrase => Assert.DoesNotContain(phrase, source, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain('\u2014', source);
    }

    [Fact]
    public void PrototypeInstantiatesWithTheAeziolThemeResources()
    {
        WpfTestHost.Run(() =>
        {
            var prototype = new Aeziol.App.DiscordSettingsV4.DiscordSettingsV4Concept1();

            Assert.IsType<WpfComboBox>(prototype.FindName("RestoreDelayComboBox"));
            Assert.IsType<WpfButton>(prototype.FindName("ManageProtectedOutputsButton"));
            Assert.IsType<WpfCheckBox>(prototype.FindName("WindowsNotificationToggle"));
            Assert.IsType<WpfButton>(prototype.FindName("ChooseDiscordExecutableButton"));
            Assert.IsType<WpfButton>(prototype.FindName("RevokeDiscordAuthorizationButton"));
        });
    }

    private static XDocument LoadPrototype() => XDocument.Load(FindSourceFile(
        "src",
        "Aeziol.App",
        "DiscordSettingsV4",
        "DiscordSettingsV4Concept1.xaml"));

    private static string FlattenText(XElement element) => string.Join(
        " ",
        element.DescendantsAndSelf()
            .Attributes("Text")
            .Select(attribute => attribute.Value)
            .Concat(element.DescendantsAndSelf()
                .Attributes("Content")
                .Select(attribute => attribute.Value)));

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
