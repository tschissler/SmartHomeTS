using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HeartbeatLib;

/// <summary>
/// Publishes a <see cref="ServiceHeartbeat"/> on a fixed cadence. For services that run on a
/// generic host; the ones built around their own loop (ChargingController, KebaConnector) call
/// <see cref="ServiceHeartbeat.BuildPayload(HealthReport, DateTimeOffset?)"/> from there instead.
///
/// The publish is a delegate rather than an MQTT client: the services run two major versions of
/// MQTTnet and two different wrappers around it.
/// </summary>
public sealed class ServiceHeartbeatWorker : BackgroundService
{
    private readonly ServiceHeartbeat _heartbeat;
    private readonly Func<CancellationToken, Task<HealthReport>> _zustand;
    private readonly Func<string, string, CancellationToken, Task> _publish;
    private readonly Func<DateTimeOffset?>? _letzteAktion;
    private readonly TimeSpan _intervall;
    private readonly ILogger? _log;

    public ServiceHeartbeatWorker(
        ServiceHeartbeat heartbeat,
        Func<CancellationToken, Task<HealthReport>> zustand,
        Func<string, string, CancellationToken, Task> publish,
        Func<DateTimeOffset?>? letzteAktion = null,
        TimeSpan? intervall = null,
        ILogger? log = null)
    {
        _heartbeat = heartbeat;
        _zustand = zustand;
        _publish = publish;
        _letzteAktion = letzteAktion;
        _intervall = intervall ?? ServiceHeartbeat.DefaultIntervall;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log?.LogInformation("Publishing the service heartbeat on {Topic} every {Seconds} s.",
            _heartbeat.Topic, _intervall.TotalSeconds);

        using var timer = new PeriodicTimer(_intervall);
        while (!stoppingToken.IsCancellationRequested)
        {
            await PublishOnceAsync(stoppingToken);
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PublishOnceAsync(CancellationToken ct)
    {
        try
        {
            var report = await _zustand(ct);
            var payload = _heartbeat.BuildPayload(report, _letzteAktion?.Invoke());
            await _publish(_heartbeat.Topic, payload, ct);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            // A heartbeat that cannot be sent must never take the service with it - a broker
            // outage already shows up as a silent card, which is exactly the intended signal.
            _log?.LogWarning("Could not publish the heartbeat on {Topic}: {Message}",
                _heartbeat.Topic, ex.Message);
        }
    }
}
