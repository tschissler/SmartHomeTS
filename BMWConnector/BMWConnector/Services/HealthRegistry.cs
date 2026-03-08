using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BMWConnector.Services;

/// <summary>
/// Tracks the BMW broker connection state for each vehicle.
/// Updated by BmwCarDataService; queried by the health check endpoint.
/// </summary>
public class HealthRegistry
{
    private readonly ConcurrentDictionary<string, bool> _connected = new();

    public void SetConnected(string vehicle, bool connected)
        => _connected[vehicle] = connected;

    public HealthCheckResult GetResult()
    {
        if (_connected.IsEmpty)
            return HealthCheckResult.Unhealthy("No vehicles registered yet.");

        var connected    = _connected.Where(kv =>  kv.Value).Select(kv => kv.Key).ToList();
        var disconnected = _connected.Where(kv => !kv.Value).Select(kv => kv.Key).ToList();

        if (disconnected.Count == 0)
            return HealthCheckResult.Healthy($"All vehicles connected: {string.Join(", ", connected)}");

        if (connected.Count > 0)
            return HealthCheckResult.Degraded(
                $"Partial: connected={string.Join(", ", connected)}  disconnected={string.Join(", ", disconnected)}");

        return HealthCheckResult.Unhealthy($"No vehicles connected: {string.Join(", ", disconnected)}");
    }
}
