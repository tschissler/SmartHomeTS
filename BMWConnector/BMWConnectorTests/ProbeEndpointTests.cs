using System.Net;
using BMWConnector.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace BMWConnectorTests;

/// <summary>
/// Drives the probe endpoints through the real ASP.NET pipeline with the production status-code
/// mapping. The bug behind backlog item 2 was invisible in every unit test: the registry
/// correctly reported Degraded, and the endpoint answered 200 anyway.
/// </summary>
public class ProbeEndpointTests
{
    [Fact]
    public void Readiness_must_not_let_a_partial_outage_pass_as_ready()
    {
        ProbeStatusCodes.Readiness()[HealthStatus.Degraded]
            .Should().Be(StatusCodes.Status503ServiceUnavailable);
    }

    [Fact]
    public void The_informational_endpoint_stays_green_on_a_warning()
    {
        ProbeStatusCodes.Informational()[HealthStatus.Degraded]
            .Should().Be(StatusCodes.Status200OK);
    }

    /// <summary>
    /// The completion criterion for backlog item 2: one of two vehicles down has to be
    /// visible from the outside, and must not restart the pod.
    /// </summary>
    [Fact]
    public async Task One_vehicle_down_makes_readiness_fail_while_liveness_stays_green()
    {
        var registry = new HealthRegistry(
            NullLogger<HealthRegistry>.Instance, TimeSpan.Zero, () => DateTimeOffset.UtcNow);
        registry.SetConnected("BMW", true);
        registry.SetConnected("Mini", true);
        registry.SetConnected("BMW", false);   // simulated outage of one of two vehicles

        using var host = await StartProbeHostAsync(registry);
        var client = host.GetTestClient();

        (await client.GetAsync("/healthz/ready")).StatusCode
            .Should().Be(HttpStatusCode.ServiceUnavailable);

        // A restart cannot renew a token, so the outage must never reach liveness.
        (await client.GetAsync("/healthz/live")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Both_vehicles_connected_makes_readiness_pass()
    {
        var registry = new HealthRegistry(
            NullLogger<HealthRegistry>.Instance, TimeSpan.Zero, () => DateTimeOffset.UtcNow);
        registry.SetConnected("BMW", true);
        registry.SetConnected("Mini", true);

        using var host = await StartProbeHostAsync(registry);

        var response = await host.GetTestClient().GetAsync("/healthz/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("BMW");
    }

    /// <summary>Mirrors the probe wiring from Program.cs, minus the Kubernetes-bound services.</summary>
    private static async Task<IHost> StartProbeHostAsync(HealthRegistry registry)
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddHealthChecks()
                        .AddCheck("process-liveness", () => HealthCheckResult.Healthy("Process is serving."), tags: ["live"])
                        .AddCheck("bmw-broker", registry.GetResult, tags: ["ready"]);
                })
                .Configure(app => app.UseRouting().UseEndpoints(endpoints =>
                {
                    endpoints.MapHealthChecks("/healthz/live", new HealthCheckOptions
                    {
                        Predicate = check => check.Tags.Contains("live"),
                    });
                    endpoints.MapHealthChecks("/healthz/ready", new HealthCheckOptions
                    {
                        Predicate = check => check.Tags.Contains("ready"),
                        ResultStatusCodes = ProbeStatusCodes.Readiness(),
                        ResponseWriter = (context, report) =>
                            context.Response.WriteAsync(report.Entries["bmw-broker"].Description ?? ""),
                    });
                })))
            .StartAsync();

        return host;
    }
}
