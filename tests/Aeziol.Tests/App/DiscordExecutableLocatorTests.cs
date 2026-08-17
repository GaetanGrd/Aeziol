using Aeziol.App.Services;

namespace Aeziol.Tests.App;

public sealed class DiscordExecutableLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aeziol.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void FindInstalledExecutable_UsesNewestDiscordInstallation()
    {
        var oldDirectory = Path.Combine(_root, "Discord", "app-1.0.9000");
        var newDirectory = Path.Combine(_root, "Discord", "app-1.0.9999");
        Directory.CreateDirectory(oldDirectory);
        Directory.CreateDirectory(newDirectory);
        File.WriteAllText(Path.Combine(oldDirectory, "Discord.exe"), string.Empty);
        var expected = Path.Combine(newDirectory, "Discord.exe");
        File.WriteAllText(expected, string.Empty);

        var actual = DiscordExecutableLocator.FindInstalledExecutable(_root);

        Assert.Equal(Path.GetFullPath(expected), actual);
    }

    [Fact]
    public void FindInstalledExecutable_ReturnsNullWhenDiscordIsAbsent()
    {
        Directory.CreateDirectory(_root);

        Assert.Null(DiscordExecutableLocator.FindInstalledExecutable(_root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
