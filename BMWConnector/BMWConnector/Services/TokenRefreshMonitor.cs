using System.Collections.Concurrent;
using System.Globalization;
using BMWConnector.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BMWConnector.Services;

/// <summary>
/// Warns while the refresh token can still be saved, instead of after it has died.
///
/// BMW's refresh token lives two weeks and rotates on every use, so the service's 50-minute
/// refresh normally keeps it indefinitely young. A stored token that stops getting younger
/// therefore means one of two things, both worth a warning a week ahead of the cliff:
/// refreshes are failing, or they succeed but are no longer reaching the Secret — in which
/// case the only valid refresh token lives in this process's memory and dies with it.
///
/// Deliberately NOT part of readiness: a connector whose stored token is stale still delivers
/// data, and failing readiness over it would take a working pod out of service.
/// The result is exposed on <c>/healthz/tokens</c> instead.
/// </summary>
public sealed class TokenRefreshMonitor : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly IReadOnlyList<VehicleConfig> _vehicles;
    private readonly ISecretStore _store;
    private readonly ILogger<TokenRefreshMonitor> _log;
    private readonly TimeSpan _staleAfter;
    private readonly ConcurrentDictionary<string, Finding> _findings = new();

    private readonly record struct Finding(string Summary, bool Stale);

    public TokenRefreshMonitor(
        IReadOnlyList<VehicleConfig> vehicles, ISecretStore store,
        ILogger<TokenRefreshMonitor> log, TimeSpan staleAfter)
    {
        _vehicles   = vehicles;
        _store      = store;
        _log        = log;
        _staleAfter = staleAfter;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await CheckAllAsync(ct);

            try { await Task.Delay(CheckInterval, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Evaluates every vehicle once. Public so a test can run a pass without a host.</summary>
    public async Task CheckAllAsync(CancellationToken ct = default)
    {
        foreach (var vehicle in _vehicles)
            await CheckAsync(vehicle.Name, ct);
    }

    private async Task CheckAsync(string vehicle, CancellationToken ct)
    {
        try
        {
            var (idToken, _) = await _store.GetTokensAsync(vehicle, ct);
            var issuedAt = TokenAge.IssuedAtOf(idToken);

            if (issuedAt is null)
            {
                _findings[vehicle] = new Finding("unknown (id_token carries no iat claim)", Stale: false);
                _log.LogInformation("[{Vehicle}] Cannot tell when the stored token was issued — no iat claim.", vehicle);
                return;
            }

            var age = DateTimeOffset.UtcNow - issuedAt.Value;
            bool stale = age >= _staleAfter;
            // Invariant: this text is read by machines on /healthz/tokens, not by a German desk.
            _findings[vehicle] = new Finding(string.Format(CultureInfo.InvariantCulture,
                "last stored refresh {0:F1} days ago ({1:yyyy-MM-dd HH:mm} UTC)",
                age.TotalDays, issuedAt.Value), stale);

            if (stale)
                _log.LogWarning(
                    "[{Vehicle}] The token stored in the Kubernetes Secret has not been refreshed for "
                  + "{Days:F1} days (issued {IssuedAt:yyyy-MM-dd HH:mm} UTC, warning above {Limit:F0}). "
                  + "BMW's refresh token expires two weeks after its last use: either the refresh is "
                  + "failing, or it succeeds without reaching the Secret. Check the log for "
                  + "'Token refresh' lines before this turns into an interactive re-bootstrap.",
                    vehicle, age.TotalDays, issuedAt.Value, _staleAfter.TotalDays);
            else
                _log.LogInformation(
                    "[{Vehicle}] Stored token refreshed {Hours:F1} h ago, well inside the {Limit:F0} day limit.",
                    vehicle, age.TotalHours, _staleAfter.TotalDays);
        }
        catch (Exception ex)
        {
            _findings[vehicle] = new Finding($"unreadable ({ex.GetType().Name})", Stale: false);
            _log.LogWarning("[{Vehicle}] Could not read the token Secret: {Message}", vehicle, ex.Message);
        }
    }

    /// <summary>Current findings for the <c>/healthz/tokens</c> endpoint.</summary>
    public HealthCheckResult GetResult()
    {
        if (_findings.IsEmpty)
            return HealthCheckResult.Healthy("Stored token age not determined yet.");

        string summary = string.Join("; ",
            _findings.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}: {kv.Value.Summary}"));

        // Degraded, never Unhealthy: a stale stored token still works until BMW says otherwise.
        return _findings.Values.Any(f => f.Stale)
            ? HealthCheckResult.Degraded(summary)
            : HealthCheckResult.Healthy(summary);
    }
}
