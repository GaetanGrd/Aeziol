using System.Collections.ObjectModel;

namespace Aeziol.App.Notifications;

internal sealed class NotificationCenter
{
    public ObservableCollection<AppNotification> Items { get; } = [];

    public AppNotification Publish(string message, NotificationSeverity severity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var notification = new AppNotification(
            Guid.NewGuid(),
            message,
            severity,
            DefaultDuration(severity));
        Items.Add(notification);
        return notification;
    }

    public bool Dismiss(Guid id)
    {
        var notification = Items.FirstOrDefault(item => item.Id == id);
        return notification is not null && Items.Remove(notification);
    }

    internal static TimeSpan DefaultDuration(NotificationSeverity severity) => severity switch
    {
        NotificationSeverity.Error => TimeSpan.FromSeconds(12),
        NotificationSeverity.Warning => TimeSpan.FromSeconds(7),
        _ => TimeSpan.FromSeconds(4.5),
    };
}
