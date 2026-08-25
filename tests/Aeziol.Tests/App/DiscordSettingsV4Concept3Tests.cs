using System.Xml.Linq;
using Aeziol.App.DiscordSettingsV4;

namespace Aeziol.Tests.App;

public sealed class DiscordSettingsV4Concept3Tests
{
    [Fact]
    public void StructureContainsExactlyTheRequestedSettings()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "DiscordSettingsV4Concept3.xaml"));
        var tags = document.Descendants()
            .Select(element => (string?)element.Attribute("Tag"))
            .Where(tag => tag is not null)
            .ToArray();

        Assert.Equal("2,9,3,8,7", string.Join(',', tags));
        Assert.Equal("2,3,7,8,9", string.Join(',', tags.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void StructureDoesNotRepeatMainInterfaceControlsOrUseFuturisticLanguage()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "DiscordSettingsV4Concept3.xaml"));
        var forbiddenPhrases = new[]
        {
            "État Discord",
            "Autoriser Discord",
            "Activer le module",
            "Déclencheur",
            "Priorité",
            "Choisir une sortie",
            "Sortie actuelle",
            "Restaurer maintenant",
            "Restauration forcée",
            "Discord -> Aeziol",
            "Discord → Aeziol",
            "cockpit",
            "score",
            "orbite",
            "prédiction",
        };

        foreach (var phrase in forbiddenPhrases)
        {
            Assert.DoesNotContain(phrase, source, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(">IA<", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\u2014', source);
    }

    [Fact]
    public void ControlCanBeInstantiatedByWpf()
    {
        WpfTestHost.Run(() =>
        {
            var control = new DiscordSettingsV4Concept3();

            Assert.NotNull(control.Content);
            Assert.True(control.MinWidth >= 600);
        });
    }
}
