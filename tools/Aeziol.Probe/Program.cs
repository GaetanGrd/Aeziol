using Aeziol.Core.Models;
using Aeziol.Infrastructure.Discord.Processes;
using Aeziol.Infrastructure.Windows.Audio;

if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
{
    Console.Error.WriteLine("Aeziol.Probe requires Windows 11.");
    return 2;
}

var command = args.FirstOrDefault() ?? "audio-list";

switch (command)
{
    case "audio-list":
        {
            var controller = new WindowsAudioRouteController();
            var roles = new HashSet<AudioRole>
            {
                AudioRole.Console,
                AudioRole.Multimedia,
                AudioRole.Communications,
            };
            var current = await controller.CaptureAsync(roles);
            var endpoints = await controller.GetRenderEndpointsAsync();

            foreach (var endpoint in endpoints.OrderBy(endpoint => endpoint.DisplayName, StringComparer.CurrentCultureIgnoreCase))
            {
                var activeRoles = roles.Where(role => string.Equals(current.Get(role), endpoint.Id, StringComparison.OrdinalIgnoreCase));
                Console.WriteLine($"[{endpoint.State}] {endpoint.DisplayName}");
                Console.WriteLine($"  id: {endpoint.Id}");
                Console.WriteLine($"  container: {endpoint.ContainerId ?? "n/a"}");
                Console.WriteLine($"  default roles: {string.Join(", ", activeRoles)}");
            }

            return 0;
        }
    case "discord-processes":
        {
            using var monitor = new DiscordProcessMonitor();
            monitor.Start();
            Console.WriteLine($"monitoring: {monitor.MonitoringMode}");
            foreach (var snapshot in monitor.Current)
            {
                Console.WriteLine($"{snapshot.Edition}: running={snapshot.IsRunning}, processes={snapshot.ProcessCount}");
            }

            return 0;
        }
    default:
        Console.Error.WriteLine("Usage: Aeziol.Probe [audio-list|discord-processes]");
        return 1;
}
