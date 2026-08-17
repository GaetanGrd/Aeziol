using System.Diagnostics;

namespace Aeziol.App.Services;

public static class DiscordExecutableLocator
{
    private static readonly (string DirectoryName, string ExecutableName)[] Installations =
    [
        ("Discord", "Discord.exe"),
        ("DiscordPTB", "DiscordPTB.exe"),
        ("DiscordCanary", "DiscordCanary.exe"),
        ("DiscordDevelopment", "DiscordDevelopment.exe"),
    ];

    public static string? Find()
    {
        var runningPath = FindRunningExecutable();
        if (runningPath is not null)
        {
            return runningPath;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return string.IsNullOrWhiteSpace(localAppData) ? null : FindInstalledExecutable(localAppData);
    }

    internal static string? FindInstalledExecutable(string localAppData)
    {
        foreach (var (directoryName, executableName) in Installations)
        {
            var root = Path.Combine(localAppData, directoryName);
            if (!Directory.Exists(root))
            {
                continue;
            }

            try
            {
                var match = Directory.EnumerateDirectories(root, "app-*", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .Select(path => Path.Combine(path, executableName))
                    .FirstOrDefault(File.Exists);
                if (match is not null)
                {
                    return Path.GetFullPath(match);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }

        return null;
    }

    private static string? FindRunningExecutable()
    {
        foreach (var (_, executableName) in Installations)
        {
            var processName = Path.GetFileNameWithoutExtension(executableName);
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        var path = process.MainModule?.FileName;
                        if (path is not null && File.Exists(path))
                        {
                            return Path.GetFullPath(path);
                        }
                    }
                    catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                    {
                    }
                }
            }
        }

        return null;
    }
}
