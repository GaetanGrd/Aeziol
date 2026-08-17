using Aeziol.App.Services;

namespace Aeziol.Tests.App;

public sealed class SingleInstanceCoordinatorTests
{
    [Fact]
    public void SecondaryInstance_SignalsTheExistingPrimaryInstance()
    {
        var instanceName = $"Aeziol.Tests.{Guid.NewGuid():N}";
        using var activationReceived = new ManualResetEventSlim();
        using var primary = new SingleInstanceCoordinator(instanceName);
        primary.ActivationRequested += (_, _) => activationReceived.Set();
        using var secondary = new SingleInstanceCoordinator(instanceName);

        Assert.True(primary.IsPrimaryInstance);
        Assert.False(secondary.IsPrimaryInstance);
        Assert.True(secondary.SignalPrimaryInstance());
        Assert.True(activationReceived.Wait(
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken));
    }
}
