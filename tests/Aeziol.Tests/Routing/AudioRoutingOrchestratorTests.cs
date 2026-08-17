using Aeziol.Core.Abstractions;
using Aeziol.Core.Models;
using Aeziol.Core.Policies;
using Aeziol.Core.Routing;

namespace Aeziol.Tests.Routing;

public sealed class AudioRoutingOrchestratorTests
{
    private static readonly IReadOnlySet<AudioRole> Roles = new HashSet<AudioRole>
    {
        AudioRole.Console,
        AudioRole.Multimedia,
        AudioRole.Communications,
    };

    [Fact]
    public async Task AppliesAndVerifiesTargetTransaction()
    {
        var fixture = new RoutingFixture();

        var result = await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);

        Assert.Equal(RoutingOutcome.Applied, result.Outcome);
        Assert.Equal("headset", fixture.Controller.Current.Get(AudioRole.Console));
        Assert.Equal(RouteTransactionState.Applied, fixture.Store.Transaction?.State);
    }

    [Fact]
    public async Task MissingTargetDoesNotMutateOrCreateJournal()
    {
        var fixture = new RoutingFixture();
        fixture.Controller.UsableEndpoints.Remove("headset");

        var result = await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);

        Assert.Equal(RoutingOutcome.TargetUnavailable, result.Outcome);
        Assert.Equal(0, fixture.Controller.ApplyCalls);
        Assert.Null(fixture.Store.Transaction);
    }

    [Fact]
    public async Task RetargetsActiveRouteWithoutRestoringOrLosingOriginalSource()
    {
        var fixture = new RoutingFixture();
        await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);

        var result = await fixture.Subject.RetargetAsync("hdmi", TestContext.Current.CancellationToken);

        Assert.Equal(RoutingOutcome.Retargeted, result.Outcome);
        Assert.Equal("hdmi", fixture.Controller.Current.Get(AudioRole.Console));
        Assert.Equal("speakers", fixture.Subject.ActiveTransaction?.Source.Get(AudioRole.Console));
        Assert.Equal("hdmi", fixture.Subject.ActiveTransaction?.TargetEndpointId);
        Assert.Equal(0, fixture.Controller.RestoreCalls);

        var restoreResult = await fixture.Subject.RestoreAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RoutingOutcome.Restored, restoreResult.Outcome);
        Assert.Equal("speakers", fixture.Controller.Current.Get(AudioRole.Console));
    }

    [Fact]
    public async Task RetargetingToSameOutputIsANoOp()
    {
        var fixture = new RoutingFixture();
        await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);
        var applyCalls = fixture.Controller.ApplyCalls;

        var result = await fixture.Subject.RetargetAsync("HEADSET", TestContext.Current.CancellationToken);

        Assert.Equal(RoutingOutcome.NoChangeNeeded, result.Outcome);
        Assert.Equal(applyCalls, fixture.Controller.ApplyCalls);
        Assert.Equal(0, fixture.Controller.RestoreCalls);
        Assert.Equal("headset", fixture.Store.Transaction?.TargetEndpointId);
    }

    [Fact]
    public async Task FailedRetargetReturnsToActiveTargetAndKeepsRestorationConfig()
    {
        var fixture = new RoutingFixture();
        await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);
        fixture.Controller.FailApply = true;

        var result = await fixture.Subject.RetargetAsync("hdmi", TestContext.Current.CancellationToken);

        Assert.Equal(RoutingOutcome.RolledBack, result.Outcome);
        Assert.Equal("headset", fixture.Controller.Current.Get(AudioRole.Console));
        Assert.Equal("speakers", fixture.Subject.ActiveTransaction?.Source.Get(AudioRole.Console));
        Assert.Equal("headset", fixture.Subject.ActiveTransaction?.TargetEndpointId);
        Assert.NotNull(fixture.Store.Transaction);
    }

    [Fact]
    public async Task ExcludedSourcePreventsAnyMutation()
    {
        var fixture = new RoutingFixture(excludedEndpoints: ["speakers"]);

        var result = await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);

        Assert.Equal(RoutingOutcome.SkippedByExclusion, result.Outcome);
        Assert.Equal(0, fixture.Controller.ApplyCalls);
    }

    [Fact]
    public async Task FailedApplyRollsBackExactSnapshot()
    {
        var fixture = new RoutingFixture();
        fixture.Controller.FailApply = true;

        var result = await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);

        Assert.Equal(RoutingOutcome.RolledBack, result.Outcome);
        Assert.Equal("speakers", fixture.Controller.Current.Get(AudioRole.Console));
        Assert.Null(fixture.Store.Transaction);
    }

    [Fact]
    public async Task FailedRollbackKeepsRecoveryJournal()
    {
        var fixture = new RoutingFixture();
        fixture.Controller.FailApply = true;
        fixture.Controller.FailRestore = true;

        var result = await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);

        Assert.Equal(RoutingOutcome.Failed, result.Outcome);
        Assert.Equal(RouteTransactionState.Failed, fixture.Store.Transaction?.State);
    }

    [Fact]
    public async Task ManualChangeAbandonsTransactionWithoutRestoring()
    {
        var fixture = new RoutingFixture();
        await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);
        fixture.Controller.SetCurrent("hdmi");

        var abandoned = await fixture.Subject.HandleDefaultEndpointChangedAsync(
            AudioRole.Console,
            "hdmi",
            TestContext.Current.CancellationToken);

        Assert.True(abandoned);
        Assert.Null(fixture.Subject.ActiveTransaction);
        Assert.Null(fixture.Store.Transaction);
        Assert.Equal("hdmi", fixture.Controller.Current.Get(AudioRole.Console));
    }

    [Fact]
    public async Task RestoreUsesRoleSpecificOriginalEndpoints()
    {
        var fixture = new RoutingFixture();
        fixture.Controller.Current = new AudioRouteSnapshot(new Dictionary<AudioRole, string>
        {
            [AudioRole.Console] = "speakers",
            [AudioRole.Multimedia] = "speakers",
            [AudioRole.Communications] = "communications",
        });
        await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);

        var result = await fixture.Subject.RestoreAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RoutingOutcome.Restored, result.Outcome);
        Assert.Equal("speakers", fixture.Controller.Current.Get(AudioRole.Console));
        Assert.Equal("communications", fixture.Controller.Current.Get(AudioRole.Communications));
        Assert.Null(fixture.Store.Transaction);
    }

    [Fact]
    public async Task DirectRestore_ForcesTheSavedRouteAfterTheCurrentRouteDiverges()
    {
        var fixture = new RoutingFixture();
        await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);
        fixture.Controller.SetCurrent("hdmi");

        var result = await fixture.Subject.RestoreAsync(TestContext.Current.CancellationToken);

        Assert.Equal(RoutingOutcome.Restored, result.Outcome);
        Assert.Equal("speakers", fixture.Controller.Current.Get(AudioRole.Multimedia));
        Assert.Null(fixture.Subject.ActiveTransaction);
        Assert.Null(fixture.Store.Transaction);
    }

    [Fact]
    public async Task RecoveryRefusesToOverwriteLaterManualChange()
    {
        var fixture = new RoutingFixture();
        await fixture.Subject.ActivateAsync(fixture.Rule, TestContext.Current.CancellationToken);
        fixture.Controller.SetCurrent("hdmi");

        var proposal = await fixture.Subject.InspectRecoveryAsync(TestContext.Current.CancellationToken);
        var result = await fixture.Subject.ResolveRecoveryAsync(
            restore: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(proposal);
        Assert.False(proposal.CurrentRouteStillMatchesTarget);
        Assert.False(proposal.CanSafelyRestore);
        Assert.Equal(RoutingOutcome.AbandonedByUser, result.Outcome);
        Assert.Equal("manual-route-change-detected", result.ErrorCode);
    }

    private sealed class RoutingFixture
    {
        public RoutingFixture(IEnumerable<string>? excludedEndpoints = null)
        {
            Controller = new FakeAudioRouteController();
            Store = new FakeTransactionStore();
            Subject = new AudioRoutingOrchestrator(
                Controller,
                Store,
                new EndpointExclusionPolicy(excludedEndpoints),
                new FakeClock(),
                Roles);
        }

        public FakeAudioRouteController Controller { get; }

        public FakeTransactionStore Store { get; }

        public AudioRoutingOrchestrator Subject { get; }

        public AutomationRule Rule { get; } = new(Guid.NewGuid(), "discord.voice", "headset", 100);
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeTransactionStore : IRouteTransactionStore
    {
        public RouteTransaction? Transaction { get; private set; }

        public Task<RouteTransaction?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Transaction);

        public Task SaveAsync(RouteTransaction transaction, CancellationToken cancellationToken = default)
        {
            Transaction = transaction;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(CancellationToken cancellationToken = default)
        {
            Transaction = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAudioRouteController : IAudioRouteController
    {
        public FakeAudioRouteController()
        {
            UsableEndpoints.UnionWith(["speakers", "communications", "headset", "hdmi"]);
        }

        public HashSet<string> UsableEndpoints { get; } = new(StringComparer.OrdinalIgnoreCase);

        public AudioRouteSnapshot Current { get; set; } = CreateSnapshot("speakers");

        public bool FailApply { get; set; }

        public bool FailRestore { get; set; }

        public int ApplyCalls { get; private set; }

        public int RestoreCalls { get; private set; }

        public Task<IReadOnlyList<AudioEndpoint>> GetRenderEndpointsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AudioEndpoint>>(UsableEndpoints
                .Select(id => new AudioEndpoint(id, id, AudioEndpointState.Active))
                .ToArray());

        public Task<AudioRouteSnapshot> CaptureAsync(
            IReadOnlySet<AudioRole> roles,
            CancellationToken cancellationToken = default) => Task.FromResult(Current);

        public Task<bool> IsEndpointUsableAsync(string endpointId, CancellationToken cancellationToken = default) =>
            Task.FromResult(UsableEndpoints.Contains(endpointId));

        public Task ApplyAsync(
            string endpointId,
            IReadOnlySet<AudioRole> roles,
            CancellationToken cancellationToken = default)
        {
            ApplyCalls++;
            if (FailApply)
            {
                Current = CreateSnapshot(endpointId);
                throw new InvalidOperationException("Injected apply failure.");
            }

            Current = new AudioRouteSnapshot(Current.Endpoints.ToDictionary(
                pair => pair.Key,
                pair => roles.Contains(pair.Key) ? endpointId : pair.Value));
            return Task.CompletedTask;
        }

        public Task RestoreAsync(AudioRouteSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            RestoreCalls++;
            if (FailRestore)
            {
                throw new InvalidOperationException("Injected restore failure.");
            }

            Current = snapshot;
            return Task.CompletedTask;
        }

        public Task<bool> VerifyAsync(
            string endpointId,
            IReadOnlySet<AudioRole> roles,
            CancellationToken cancellationToken = default) => Task.FromResult(
                roles.All(role => string.Equals(Current.Get(role), endpointId, StringComparison.OrdinalIgnoreCase)));

        public Task<bool> VerifyAsync(AudioRouteSnapshot snapshot, CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot.Endpoints.All(pair =>
                string.Equals(Current.Get(pair.Key), pair.Value, StringComparison.OrdinalIgnoreCase)));

        public void SetCurrent(string endpointId) => Current = CreateSnapshot(endpointId);

        private static AudioRouteSnapshot CreateSnapshot(string endpointId) => new(
            Roles.ToDictionary(role => role, _ => endpointId));
    }
}
