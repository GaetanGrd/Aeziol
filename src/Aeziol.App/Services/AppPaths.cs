namespace Aeziol.App.Services;

public sealed record AppPaths(string DataDirectory)
{
    public static AppPaths CreateDefault()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new AppPaths(Path.Combine(root, "Aeziol"));
    }

    public string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public string SettingsBackupFile => SettingsFile + ".backup";

    public string TransactionFile => Path.Combine(DataDirectory, "route-transaction.json");

    public string LogsDirectory => Path.Combine(DataDirectory, "logs");

    public string LanguagesDirectory => Path.Combine(DataDirectory, "languages");
}
