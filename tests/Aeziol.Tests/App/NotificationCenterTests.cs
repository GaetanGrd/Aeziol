using Aeziol.App.Notifications;

namespace Aeziol.Tests.App;

public sealed class NotificationCenterTests
{
    [Fact]
    public void MultipleNotificationsRemainAvailableTogether()
    {
        var center = new NotificationCenter();

        var first = center.Publish("First", NotificationSeverity.Success);
        var second = center.Publish("Second", NotificationSeverity.Warning);

        Assert.Equal([first, second], center.Items);
    }

    [Fact]
    public void DismissingOneNotificationKeepsTheOthers()
    {
        var center = new NotificationCenter();
        var first = center.Publish("First", NotificationSeverity.Success);
        var second = center.Publish("Second", NotificationSeverity.Warning);

        Assert.True(center.Dismiss(first.Id));
        Assert.Equal([second], center.Items);
    }

    [Fact]
    public void ErrorsRemainVisibleLongerThanWarningsAndNormalMessages()
    {
        var normal = NotificationCenter.DefaultDuration(NotificationSeverity.Success);
        var warning = NotificationCenter.DefaultDuration(NotificationSeverity.Warning);
        var error = NotificationCenter.DefaultDuration(NotificationSeverity.Error);

        Assert.True(warning > normal);
        Assert.True(error > warning);
    }
}
