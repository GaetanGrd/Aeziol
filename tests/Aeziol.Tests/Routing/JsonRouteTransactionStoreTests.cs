using Aeziol.Core.Models;
using Aeziol.Core.Persistence;

namespace Aeziol.Tests.Routing;

public sealed class JsonRouteTransactionStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "Aeziol.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LoadAsync_ReadsPersistedAudioRoles()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "route-transaction.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "id": "27bf02c7-14e9-42e4-87d1-f3116247eff7",
              "source": {
                "endpoints": {
                  "console": "speakers",
                  "multimedia": "speakers",
                  "communications": "headset"
                }
              },
              "targetEndpointId": "speakers",
              "roles": [
                "console",
                "multimedia",
                "communications"
              ],
              "state": "applied",
              "createdAt": "2026-08-25T14:21:33.7421741+00:00",
              "updatedAt": "2026-08-25T14:21:33.7884145+00:00",
              "failureCode": null
            }
            """,
            TestContext.Current.CancellationToken);

        using var store = new JsonRouteTransactionStore(path);

        var transaction = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(transaction);
        Assert.Equal(RouteTransactionState.Applied, transaction.State);
        Assert.Equal("speakers", transaction.Source.Get(AudioRole.Multimedia));
        Assert.Equal(
            new HashSet<AudioRole>
            {
                AudioRole.Console,
                AudioRole.Multimedia,
                AudioRole.Communications,
            },
            transaction.Roles);
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsAudioRoles()
    {
        var path = Path.Combine(_root, "route-transaction.json");
        using var store = new JsonRouteTransactionStore(path);
        var now = DateTimeOffset.UtcNow;
        var expected = new RouteTransaction(
            Guid.NewGuid(),
            new AudioRouteSnapshot(new Dictionary<AudioRole, string>
            {
                [AudioRole.Console] = "speakers",
                [AudioRole.Multimedia] = "speakers",
                [AudioRole.Communications] = "headset",
            }),
            "speakers",
            new HashSet<AudioRole>
            {
                AudioRole.Console,
                AudioRole.Multimedia,
                AudioRole.Communications,
            },
            RouteTransactionState.Applied,
            now,
            now);

        await store.SaveAsync(expected, TestContext.Current.CancellationToken);
        var actual = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(actual);
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Roles, actual.Roles);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
