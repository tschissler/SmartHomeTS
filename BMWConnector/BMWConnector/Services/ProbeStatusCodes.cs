using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BMWConnector.Services;

/// <summary>
/// How a health status becomes an HTTP status code for each probe.
/// Kept here rather than inline in Program.cs so the mapping itself is testable —
/// the bug this fixes was precisely that <see cref="HealthStatus.Degraded"/> answered 200.
/// </summary>
public static class ProbeStatusCodes
{
    /// <summary>
    /// Readiness: a partial outage is not operational. One of two vehicles connected means
    /// the connector delivers half its data, and the probe must say so.
    /// </summary>
    public static Dictionary<HealthStatus, int> Readiness() => new()
    {
        [HealthStatus.Healthy]   = StatusCodes.Status200OK,
        [HealthStatus.Degraded]  = StatusCodes.Status503ServiceUnavailable,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    };

    /// <summary>
    /// Informational endpoints (token age): <see cref="HealthStatus.Degraded"/> is a warning,
    /// not an outage. An old token works until BMW rejects it, so this must stay 200 —
    /// it is never wired to a Kubernetes probe.
    /// </summary>
    public static Dictionary<HealthStatus, int> Informational() => new()
    {
        [HealthStatus.Healthy]   = StatusCodes.Status200OK,
        [HealthStatus.Degraded]  = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    };
}
