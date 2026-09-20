using System.Collections.Concurrent;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BMWConnector.Services;

/// <summary>
/// Tracks the BMW broker connection state for each vehicle. Updated by BmwCarDataService,
/// queried by the readiness endpoint.
///
/// A vehicle counts as connected while it either holds a connection or held one within
/// <see cref="_staleAfter"/>. That grace period matters: the service tears the BMW connection
/// down and rebuilds it on every 50-minute token refresh, and without it each of those routine
/// reconnects would flap the readiness probe and cry wolf in the log.
/// </summary>
public class HealthRegistry
{
    private readonly record struct VehicleState(bool Connected, DateTimeOffset? LastConnectedAt);

    private readonly ConcurrentDictionary<string, VehicleState> _vehicles = new();
    private readonly ILogger<HealthRegistry> _log;
    private readonly TimeSpan _staleAfter;
    private readonly Func<DateTimeOffset> _now;
    private long _lastPublishedTicks;

    public HealthRegistry(ILogger<HealthRegistry> log, TimeSpan staleAfter, Func<DateTimeOffset>? now = null)
    {
        _log        = log;
        _staleAfter = staleAfter;
        _now        = now ?? (() => DateTimeOffset.UtcNow);
    }

    public void SetConnected(string vehicle, bool connected)
    {
        bool known = _vehicles.TryGetValue(vehicle, out var previous);

        _vehicles[vehicle] = new VehicleState(
            connected,
            connected ? _now() : previous.LastConnectedAt);

        if (!known)
        {
            _log.LogInformation("[{Vehicle}] Registered, not connected to the BMW broker yet.", vehicle);
            return;
        }

        if (previous.Connected == connected) return;

        // The Aug 2026 outage stayed invisible for 35 days because health was only ever logged
        // while already broken — there was no line that said "this works". This is that line.
        if (connected)
            _log.LogInformation("[{Vehicle}] Connected to the BMW broker — vehicle is ready.", vehicle);
        else
            _log.LogInformation("[{Vehicle}] Disconnected from the BMW broker, reconnecting. "
                              + "Readiness holds for {Grace} minutes.", vehicle, _staleAfter.TotalMinutes);
    }

    /// <summary>
    /// A vehicle state was published to the local broker. Recorded here rather than in a counter
    /// of its own so the service heartbeat on status/ and this registry describe the same service.
    /// </summary>
    public void MarkPublished()
        => Interlocked.Exchange(ref _lastPublishedTicks, _now().UtcTicks);

    /// <summary>When any vehicle was last published; null before the first one.</summary>
    public DateTimeOffset? LastPublishedAt
    {
        get
        {
            var ticks = Interlocked.Read(ref _lastPublishedTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    /// <summary>
    /// Readiness: every vehicle must be connected or within its grace period.
    /// <see cref="HealthStatus.Degraded"/> means a partial outage and is mapped to HTTP 503 by
    /// the readiness endpoint — a connector delivering one of two vehicles is not operational.
    /// </summary>
    public HealthCheckResult GetResult()
    {
        if (_vehicles.IsEmpty)
            return HealthCheckResult.Unhealthy("No vehicles registered yet.");

        var now = _now();
        var healthy = new List<string>();
        var stale   = new List<string>();

        foreach (var (vehicle, state) in _vehicles.OrderBy(kv => kv.Key))
        {
            if (state.Connected || (state.LastConnectedAt is { } last && now - last <= _staleAfter))
                healthy.Add(vehicle);
            else
                stale.Add(Describe(vehicle, state, now));
        }

        if (stale.Count == 0)
            return HealthCheckResult.Healthy($"All vehicles connected: {string.Join(", ", healthy)}");

        if (healthy.Count > 0)
            return HealthCheckResult.Degraded(
                $"Partial outage — connected: {string.Join(", ", healthy)}; not connected: {string.Join("; ", stale)}");

        return HealthCheckResult.Unhealthy($"No vehicle connected: {string.Join("; ", stale)}");
    }

    private static string Describe(string vehicle, VehicleState state, DateTimeOffset now)
        => state.LastConnectedAt is { } last
            ? $"{vehicle} (last connected {(now - last).TotalMinutes:F0} min ago)"
            : $"{vehicle} (never connected)";
}
