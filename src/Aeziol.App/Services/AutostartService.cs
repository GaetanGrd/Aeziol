using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Aeziol.App.Services;

public static class AutostartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupTaskId = "AeziolStartup";

    public static async Task SetEnabledAsync(bool enabled)
    {
        if (IsPackaged())
        {
            var startupTask = await StartupTask.GetAsync(StartupTaskId);
            if (enabled)
            {
                if (startupTask.State is StartupTaskState.Disabled or StartupTaskState.DisabledByUser)
                {
                    await startupTask.RequestEnableAsync();
                }
            }
            else if (startupTask.State == StartupTaskState.Enabled)
            {
                startupTask.Disable();
            }

            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Unable to open the Windows startup registry key.");
        if (enabled)
        {
            var executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("Unable to determine the Aeziol executable path.");
            key.SetValue("Aeziol", $"\"{executable}\" --background", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue("Aeziol", throwOnMissingValue: false);
        }
    }

    private static bool IsPackaged()
    {
        try
        {
            _ = Package.Current.Id;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
