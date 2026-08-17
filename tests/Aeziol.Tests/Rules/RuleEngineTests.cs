using Aeziol.Core.Models;
using Aeziol.Core.Rules;

namespace Aeziol.Tests.Rules;

public sealed class RuleEngineTests
{
    [Fact]
    public void SelectsHighestPriorityEnabledRule()
    {
        var low = new AutomationRule(Guid.NewGuid(), "discord.voice", "speakers", 10);
        var high = new AutomationRule(Guid.NewGuid(), "discord.voice", "headset", 20);

        var result = RuleEngine.Evaluate("discord.voice", [low, high]);

        Assert.Equal(high, result.SelectedRule);
        Assert.False(result.HasPriorityConflict);
    }

    [Fact]
    public void EqualHighestPrioritiesAreReportedAsConflict()
    {
        var first = new AutomationRule(Guid.NewGuid(), "discord.voice", "speakers", 20);
        var second = new AutomationRule(Guid.NewGuid(), "discord.voice", "headset", 20);

        var result = RuleEngine.Evaluate("discord.voice", [first, second]);

        Assert.Null(result.SelectedRule);
        Assert.True(result.HasPriorityConflict);
    }
}
