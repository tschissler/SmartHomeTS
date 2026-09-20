using BMWConnector.Services;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;

namespace BMWConnectorTests;

/// <summary>
/// The readiness semantics from backlog item 2: a partial outage must be visible, and the
/// routine reconnect every 50 minutes must not be mistaken for one.
/// </summary>
public class HealthRegistryTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    private DateTimeOffset _now = new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private HealthRegistry NewRegistry() =>
        new(NullLogger<HealthRegistry>.Instance, Grace, () => _now);

    private void Advance(TimeSpan by) => _now += by;

    [Fact]
    public void Without_any_vehicle_the_connector_is_not_ready()
    {
        NewRegistry().GetResult().Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public void A_registered_but_never_connected_vehicle_is_not_ready()
    {
        var registry = NewRegistry();
        registry.SetConnected("BMW", false);

        var result = registry.GetResult();

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("never connected");
    }

    [Fact]
    public void Both_vehicles_connected_is_healthy()
    {
        var registry = NewRegistry();
        registry.SetConnected("BMW", true);
        registry.SetConnected("Mini", true);

        registry.GetResult().Status.Should().Be(HealthStatus.Healthy);
    }

    /// <summary>
    /// The completion criterion for backlog item 2: one of two vehicles failing has to show.
    /// </summary>
    [Fact]
    public void One_of_two_vehicles_failing_is_degraded_and_names_the_vehicle()
    {
        var registry = NewRegistry();
        registry.SetConnected("BMW", true);
        registry.SetConnected("Mini", true);

        registry.SetConnected("BMW", false);
        Advance(Grace + TimeSpan.FromMinutes(1));

        var result = registry.GetResult();

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("BMW").And.Contain("Mini");
    }

    [Fact]
    public void A_routine_reconnect_inside_the_grace_period_stays_healthy()
    {
        // Every 50 minutes the service tears the BMW connection down and rebuilds it.
        // Without the grace period that would flap readiness roughly 29 times a day per vehicle.
        var registry = NewRegistry();
        registry.SetConnected("BMW", true);
        registry.SetConnected("Mini", true);

        registry.SetConnected("BMW", false);
        Advance(TimeSpan.FromSeconds(30));

        registry.GetResult().Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void Reconnecting_clears_a_previous_outage()
    {
        var registry = NewRegistry();
        registry.SetConnected("BMW", true);
        registry.SetConnected("BMW", false);
        Advance(Grace + TimeSpan.FromMinutes(10));
        registry.GetResult().Status.Should().Be(HealthStatus.Unhealthy);

        registry.SetConnected("BMW", true);

        registry.GetResult().Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void Both_vehicles_failing_is_unhealthy()
    {
        var registry = NewRegistry();
        registry.SetConnected("BMW", true);
        registry.SetConnected("Mini", true);
        registry.SetConnected("BMW", false);
        registry.SetConnected("Mini", false);
        Advance(Grace + TimeSpan.FromMinutes(1));

        registry.GetResult().Status.Should().Be(HealthStatus.Unhealthy);
    }
}
