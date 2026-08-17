using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aeziol.App.Settings;

namespace Aeziol.App.Services;

internal sealed record AppUpdateRelease(
    ReleaseVersion Version,
    string TagName,
    bool IsPrerelease,
    Uri ReleasePageUri,
    Uri PackageUri,
    Uri ChecksumUri,
    string PackageFileName);

internal readonly record struct ReleaseVersion(int Major, int Minor, int Patch, int? BetaRevision)
    : IComparable<ReleaseVersion>
{
    private static readonly Regex Pattern = new(
        @"^v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-beta\.(?<beta>\d+))?$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public bool IsPrerelease => BetaRevision.HasValue;

    public static bool TryParse(string? value, out ReleaseVersion version)
    {
        var match = Pattern.Match(value?.Trim() ?? string.Empty);
        if (!match.Success
            || !int.TryParse(match.Groups["major"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(match.Groups["minor"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var minor)
            || !int.TryParse(match.Groups["patch"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var patch))
        {
            version = default;
            return false;
        }

        int? beta = null;
        if (match.Groups["beta"].Success)
        {
            if (!int.TryParse(match.Groups["beta"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedBeta))
            {
                version = default;
                return false;
            }

            if (parsedBeta is < 1 or > 65534)
            {
                version = default;
                return false;
            }

            beta = parsedBeta;
        }

        version = new ReleaseVersion(major, minor, patch, beta);
        return true;
    }

    public int CompareTo(ReleaseVersion other)
    {
        var result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        if (BetaRevision is null)
        {
            return other.BetaRevision is null ? 0 : 1;
        }

        return other.BetaRevision is null ? -1 : BetaRevision.Value.CompareTo(other.BetaRevision.Value);
    }

    public override string ToString() => BetaRevision is { } beta
        ? $"{Major}.{Minor}.{Patch}-beta.{beta}"
        : $"{Major}.{Minor}.{Patch}";
}

internal sealed class AppUpdateService
{
    private const long MaximumPackageBytes = 512L * 1024 * 1024;
    private const int MaximumChecksumBytes = 4096;
    private static readonly Uri DefaultReleasesUri = new(
        "https://api.github.com/repos/GaetanGrd/Aeziol/releases?per_page=30");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly string _updatesDirectory;
    private readonly Uri _releasesUri;

    public AppUpdateService(HttpClient httpClient, string updatesDirectory, Uri? releasesUri = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _updatesDirectory = Path.GetFullPath(updatesDirectory);
        _releasesUri = releasesUri ?? DefaultReleasesUri;
    }

    public async Task<AppUpdateRelease?> CheckAsync(
        string currentVersion,
        UpdateChannel channel,
        CancellationToken cancellationToken = default)
    {
        if (!ReleaseVersion.TryParse(currentVersion, out var installedVersion))
        {
            throw new InvalidDataException($"Unsupported installed Aeziol version: {currentVersion}");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, _releasesUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Aeziol", installedVersion.ToString()));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var releases = await JsonSerializer.DeserializeAsync<GitHubRelease[]>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false) ?? [];

        return releases
            .Where(release => !release.Draft)
            .Select(ToUpdateRelease)
            .Where(release => release is not null)
            .Select(release => release!)
            .Where(release => channel == UpdateChannel.Beta || !release.IsPrerelease)
            .Where(release => release.Version.CompareTo(installedVersion) > 0)
            .OrderByDescending(release => release.Version)
            .FirstOrDefault();
    }

    public async Task<string> DownloadAsync(
        AppUpdateRelease release,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(release);
        ValidateDownloadUri(release.PackageUri);
        ValidateDownloadUri(release.ChecksumUri);
        if (!string.Equals(Path.GetFileName(release.PackageFileName), release.PackageFileName, StringComparison.Ordinal)
            || !release.PackageFileName.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The release contains an invalid package file name.");
        }

        var expectedHash = await DownloadChecksumAsync(
            release.ChecksumUri,
            release.PackageFileName,
            cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(_updatesDirectory);
        var destinationPath = Path.Combine(_updatesDirectory, release.PackageFileName);
        var temporaryPath = destinationPath + ".download";
        File.Delete(temporaryPath);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, release.PackageUri);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Aeziol", ApplicationVersion.Current));
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is > MaximumPackageBytes)
            {
                throw new InvalidDataException("The update package is unexpectedly large.");
            }

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (var destination = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81920];
                long totalBytes = 0;
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > MaximumPackageBytes)
                    {
                        throw new InvalidDataException("The update package exceeded the allowed size.");
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                    if (response.Content.Headers.ContentLength is > 0)
                    {
                        progress?.Report((double)totalBytes / response.Content.Headers.ContentLength.Value);
                    }
                }

                await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            string actualHash;
            await using (var downloaded = new FileStream(
                temporaryPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                actualHash = Convert.ToHexString(
                    await SHA256.HashDataAsync(downloaded, cancellationToken).ConfigureAwait(false));
            }

            if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The downloaded update did not match its published SHA-256 checksum.");
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
            DeleteOlderPackages(destinationPath);
            progress?.Report(1);
            return destinationPath;
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    public static void LaunchInstaller(string packagePath)
    {
        var fullPath = Path.GetFullPath(packagePath);
        if (!File.Exists(fullPath) || !fullPath.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException("The downloaded MSIX package is unavailable.", fullPath);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = fullPath,
            UseShellExecute = true,
        });
    }

    private async Task<string> DownloadChecksumAsync(
        Uri checksumUri,
        string packageFileName,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, checksumUri);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Aeziol", ApplicationVersion.Current));
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaximumChecksumBytes)
        {
            throw new InvalidDataException("The update checksum is unexpectedly large.");
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (bytes.Length > MaximumChecksumBytes)
        {
            throw new InvalidDataException("The update checksum exceeded the allowed size.");
        }

        var line = Encoding.UTF8.GetString(bytes).Trim();
        var match = Regex.Match(
            line,
            @"^(?<hash>[0-9a-fA-F]{64})\s+\*?(?<name>[^\r\n]+)$",
            RegexOptions.CultureInvariant);
        if (!match.Success
            || !string.Equals(match.Groups["name"].Value.Trim(), packageFileName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The release checksum has an invalid format or package name.");
        }

        return match.Groups["hash"].Value;
    }

    private static AppUpdateRelease? ToUpdateRelease(GitHubRelease release)
    {
        if (!ReleaseVersion.TryParse(release.TagName, out var version)
            || version.IsPrerelease != release.Prerelease
            || !Uri.TryCreate(release.HtmlUrl, UriKind.Absolute, out var releasePageUri))
        {
            return null;
        }

        var packageFileName = $"Aeziol-{version}-x64.msix";
        var package = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, packageFileName, StringComparison.OrdinalIgnoreCase));
        var checksum = release.Assets.FirstOrDefault(asset =>
            string.Equals(asset.Name, packageFileName + ".sha256", StringComparison.OrdinalIgnoreCase));
        if (package is null
            || checksum is null
            || !Uri.TryCreate(package.BrowserDownloadUrl, UriKind.Absolute, out var packageUri)
            || !Uri.TryCreate(checksum.BrowserDownloadUrl, UriKind.Absolute, out var checksumUri))
        {
            return null;
        }

        return new AppUpdateRelease(
            version,
            release.TagName,
            release.Prerelease,
            releasePageUri,
            packageUri,
            checksumUri,
            packageFileName);
    }

    private static void ValidateDownloadUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri
            || uri.Scheme != Uri.UriSchemeHttps
            || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Aeziol refused an update download outside GitHub HTTPS.");
        }
    }

    private void DeleteOlderPackages(string currentPackagePath)
    {
        foreach (var path in Directory.EnumerateFiles(_updatesDirectory, "Aeziol-*-x64.msix"))
        {
            if (string.Equals(path, currentPackagePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed record GitHubRelease(
        [property: JsonPropertyName("tag_name")] string TagName,
        [property: JsonPropertyName("draft")] bool Draft,
        [property: JsonPropertyName("prerelease")] bool Prerelease,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("assets")] GitHubAsset[] Assets);

    private sealed record GitHubAsset(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
