using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Aeziol.App.Services;
using Aeziol.App.Settings;

namespace Aeziol.Tests.App;

public sealed class AppUpdateServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aeziol.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("0.9.0-beta.1", "0.9.0-beta.2", -1)]
    [InlineData("0.9.0-beta.8", "0.9.0", -1)]
    [InlineData("1.0.0", "0.99.0", 1)]
    [InlineData("v1.2.3", "1.2.3", 0)]
    public void ReleaseVersion_UsesStableAfterBetas(
        string leftText,
        string rightText,
        int expectedSign)
    {
        Assert.True(ReleaseVersion.TryParse(leftText, out var left));
        Assert.True(ReleaseVersion.TryParse(rightText, out var right));

        Assert.Equal(expectedSign, Math.Sign(left.CompareTo(right)));
    }

    [Theory]
    [InlineData("1.0.0-beta.0")]
    [InlineData("1.0.0-beta.65535")]
    [InlineData("1.0.0-preview.1")]
    public void ReleaseVersion_RejectsUnsupportedPublicTags(string value) =>
        Assert.False(ReleaseVersion.TryParse(value, out _));

    [Fact]
    public async Task CheckAsync_StableChannelIgnoresPrereleases()
    {
        var handler = new FakeHttpMessageHandler(request => JsonResponse(
            """
            [
              {
                "tag_name": "v1.1.0-beta.2",
                "draft": false,
                "prerelease": true,
                "html_url": "https://github.com/GaetanGrd/Aeziol/releases/tag/v1.1.0-beta.2",
                "assets": [
                  { "name": "Aeziol-1.1.0-beta.2-x64.msix", "browser_download_url": "https://github.com/download/beta.msix" },
                  { "name": "Aeziol-1.1.0-beta.2-x64.msix.sha256", "browser_download_url": "https://github.com/download/beta.sha256" }
                ]
              },
              {
                "tag_name": "v1.0.1",
                "draft": false,
                "prerelease": false,
                "html_url": "https://github.com/GaetanGrd/Aeziol/releases/tag/v1.0.1",
                "assets": [
                  { "name": "Aeziol-1.0.1-x64.msix", "browser_download_url": "https://github.com/download/stable.msix" },
                  { "name": "Aeziol-1.0.1-x64.msix.sha256", "browser_download_url": "https://github.com/download/stable.sha256" }
                ]
              }
            ]
            """));
        var service = CreateService(handler);

        var update = await service.CheckAsync("1.0.0", UpdateChannel.Stable, TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        Assert.Equal("1.0.1", update.Version.ToString());
        Assert.False(update.IsPrerelease);
    }

    [Fact]
    public async Task CheckAsync_BetaChannelAcceptsPrereleasesButIgnoresDraftsAndIncompleteAssets()
    {
        var handler = new FakeHttpMessageHandler(request => JsonResponse(
            """
            [
              {
                "tag_name": "v2.0.0-beta.9",
                "draft": true,
                "prerelease": true,
                "html_url": "https://github.com/GaetanGrd/Aeziol/releases/tag/v2.0.0-beta.9",
                "assets": []
              },
              {
                "tag_name": "v1.2.0-beta.2",
                "draft": false,
                "prerelease": true,
                "html_url": "https://github.com/GaetanGrd/Aeziol/releases/tag/v1.2.0-beta.2",
                "assets": [
                  { "name": "Aeziol-1.2.0-beta.2-x64.msix", "browser_download_url": "https://github.com/download/beta.msix" },
                  { "name": "Aeziol-1.2.0-beta.2-x64.msix.sha256", "browser_download_url": "https://github.com/download/beta.sha256" }
                ]
              },
              {
                "tag_name": "v1.3.0-beta.1",
                "draft": false,
                "prerelease": true,
                "html_url": "https://github.com/GaetanGrd/Aeziol/releases/tag/v1.3.0-beta.1",
                "assets": [
                  { "name": "Aeziol-1.3.0-beta.1-x64.msix", "browser_download_url": "https://github.com/download/incomplete.msix" }
                ]
              }
            ]
            """));
        var service = CreateService(handler);

        var update = await service.CheckAsync("1.2.0-beta.1", UpdateChannel.Beta, TestContext.Current.CancellationToken);

        Assert.NotNull(update);
        Assert.Equal("1.2.0-beta.2", update.Version.ToString());
    }

    [Fact]
    public async Task DownloadAsync_VerifiesChecksumBeforeKeepingPackage()
    {
        var package = Encoding.UTF8.GetBytes("signed-msix-fixture");
        var hash = Convert.ToHexString(SHA256.HashData(package)).ToLowerInvariant();
        var handler = new FakeHttpMessageHandler(request => request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{hash}  Aeziol-1.0.1-x64.msix"),
            }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(package),
            });
        var service = CreateService(handler);
        var release = CreateRelease();

        var path = await service.DownloadAsync(release, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(package, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.False(File.Exists(path + ".download"));
    }

    [Fact]
    public async Task DownloadAsync_DeletesPackageWhenChecksumDoesNotMatch()
    {
        var handler = new FakeHttpMessageHandler(request => request.RequestUri!.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{new string('0', 64)}  Aeziol-1.0.1-x64.msix"),
            }
            : new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes("tampered")),
            });
        var service = CreateService(handler);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.DownloadAsync(CreateRelease(), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(Directory.Exists(_root) ? Directory.GetFiles(_root) : []);
    }

    private AppUpdateService CreateService(HttpMessageHandler handler) => new(
        new HttpClient(handler),
        _root,
        new Uri("https://example.test/releases"));

    private static AppUpdateRelease CreateRelease() => new(
        new ReleaseVersion(1, 0, 1, null),
        "v1.0.1",
        false,
        new Uri("https://github.com/GaetanGrd/Aeziol/releases/tag/v1.0.1"),
        new Uri("https://github.com/download/Aeziol-1.0.1-x64.msix"),
        new Uri("https://github.com/download/Aeziol-1.0.1-x64.msix.sha256"),
        "Aeziol-1.0.1-x64.msix");

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responder(request));
    }
}
