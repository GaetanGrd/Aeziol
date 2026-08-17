namespace Aeziol.App.Notifications;

public enum NotificationSeverity
{
    Information,
    Success,
    Warning,
    Error,
}

public sealed record AppNotification(
    Guid Id,
    string Message,
    NotificationSeverity Severity,
    TimeSpan DisplayDuration);
