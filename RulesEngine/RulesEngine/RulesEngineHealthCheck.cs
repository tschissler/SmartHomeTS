using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RulesEngine;

public class RulesEngineHealthCheck : IHealthCheck
{
    private static DateTime _lastEvaluation = DateTime.MinValue;
    private static bool _isMqttConnected = false;

    public static void UpdateLastEvaluation()
    {
        _lastEvaluation = DateTime.UtcNow;
    }

    public static void UpdateMqttConnectionStatus(bool isConnected)
    {
        _isMqttConnected = isConnected;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // The rule is evaluated every minute, so a few missed cycles mean trouble
        var timeSinceLastEvaluation = DateTime.UtcNow - _lastEvaluation;
        var isHealthy = _isMqttConnected &&
                       (_lastEvaluation == DateTime.MinValue || timeSinceLastEvaluation.TotalMinutes < 5);

        if (isHealthy)
        {
            var message = _lastEvaluation == DateTime.MinValue
                ? "Service is starting up"
                : $"Last rule evaluation: {timeSinceLastEvaluation.TotalSeconds:F0} seconds ago";

            return Task.FromResult(HealthCheckResult.Healthy(message));
        }

        var unhealthyMessage = !_isMqttConnected
            ? "MQTT not connected"
            : $"No rule evaluation for {timeSinceLastEvaluation.TotalMinutes:F1} minutes";

        return Task.FromResult(HealthCheckResult.Unhealthy(unhealthyMessage));
    }
}
