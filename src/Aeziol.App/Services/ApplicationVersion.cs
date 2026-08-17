using System.Reflection;

namespace Aeziol.App.Services;

internal static class ApplicationVersion
{
    public static string Current
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            return Normalize(informational, assembly.GetName().Version);
        }
    }

    internal static string Normalize(string? informationalVersion, Version? fallback)
    {
        var normalized = informationalVersion?.Split('+', 2)[0].Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? fallback?.ToString(3) ?? "0.1.0"
            : normalized;
    }
}
