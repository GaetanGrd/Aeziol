using Aeziol.Infrastructure.Discord.Voice;

namespace Aeziol.Tests.Discord;

public sealed class DiscordRetryScheduleTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PipeFailureBlocksOnlyThatPipeUntilTheConnectionDelayExpires()
    {
        var time = new MutableTimeProvider(Now);
        var schedule = new DiscordRetrySchedule(time, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(2));

        schedule.MarkPipeFailure(3);

        Assert.False(schedule.CanAttemptPipe(3));
        Assert.True(schedule.CanAttemptPipe(4));
        time.UtcNow = Now + TimeSpan.FromSeconds(10);
        Assert.True(schedule.CanAttemptPipe(3));
    }

    [Fact]
    public void AuthorizationFailureBlocksEveryAuthorizationAttemptUntilItsDelayExpires()
    {
        var time = new MutableTimeProvider(Now);
        var schedule = new DiscordRetrySchedule(time, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(2));

        schedule.MarkAuthorizationFailure();

        Assert.False(schedule.CanAttemptAuthorization);
        time.UtcNow = Now + TimeSpan.FromMinutes(2);
        Assert.True(schedule.CanAttemptAuthorization);
    }

    [Fact]
    public void SuccessfulSessionClearsBothPipeAndAuthorizationBackoff()
    {
        var time = new MutableTimeProvider(Now);
        var schedule = new DiscordRetrySchedule(time, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(2));
        schedule.MarkPipeFailure(2);
        schedule.MarkAuthorizationFailure();

        schedule.MarkSuccess(2);

        Assert.True(schedule.CanAttemptPipe(2));
        Assert.True(schedule.CanAttemptAuthorization);
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
