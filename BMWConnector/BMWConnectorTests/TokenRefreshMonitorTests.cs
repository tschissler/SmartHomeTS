using BMWConnector.Models;
using BMWConnector.Services;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace BMWConnectorTests;

public class TokenRefreshMonitorTests
{
    private static TokenRefreshMonitor MonitorFor(ISecretStore store, TimeSpan staleAfter)
        => new([new VehicleConfig { Name = "BMW" }], store,
               NullLogger<TokenRefreshMonitor>.Instance, staleAfter);

    [Fact]
    public void Before_the_first_check_nothing_is_claimed()
    {
        MonitorFor(new FakeSecretStore(), TimeSpan.FromDays(7))
            .GetResult().Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task A_freshly_refreshed_stored_token_is_healthy()
    {
        var store = new FakeSecretStore()
            .WithTokens("BMW", TokenAgeTests.Jwt(iat: DateTimeOffset.UtcNow.AddMinutes(-20)));
        var monitor = MonitorFor(store, TimeSpan.FromDays(7));

        await monitor.CheckAllAsync();

        var result = monitor.GetResult();
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("BMW");
    }

    /// <summary>
    /// The refresh token rotates and dies two weeks after its last use. A stored token that
    /// stopped getting younger means the refresh is failing, or succeeding without reaching
    /// the Secret — in which case a pod restart would lose the only valid token.
    /// </summary>
    [Fact]
    public async Task A_stored_token_that_stopped_being_refreshed_is_reported_as_degraded()
    {
        var store = new FakeSecretStore()
            .WithTokens("BMW", TokenAgeTests.Jwt(iat: DateTimeOffset.UtcNow.AddDays(-9)));
        var monitor = MonitorFor(store, TimeSpan.FromDays(7));

        await monitor.CheckAllAsync();

        var result = monitor.GetResult();
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("9.0 days ago");
    }

    [Fact]
    public async Task An_unreadable_secret_does_not_masquerade_as_a_stale_token()
    {
        var monitor = MonitorFor(new FakeSecretStore(), TimeSpan.FromDays(7));

        await monitor.CheckAllAsync();

        var result = monitor.GetResult();
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("unreadable");
    }
}
