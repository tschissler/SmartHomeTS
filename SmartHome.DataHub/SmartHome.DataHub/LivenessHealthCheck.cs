using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SmartHome.DataHub
{
    /// <summary>
    /// Liveness check: only verifies that the process itself is still able to work.
    /// Downstream outages (InfluxDB, MQTT broker) must NOT restart the pod — a restart
    /// discards the in-memory write buffer that bridges exactly those outages.
    /// The single exception: the MQTT client is stuck despite its auto-reconnect.
    /// </summary>
    public class LivenessHealthCheck : IHealthCheck
    {
        private readonly MQTTClient.MQTTClient _mqttClient;

        public LivenessHealthCheck(MQTTClient.MQTTClient mqttClient)
        {
            _mqttClient = mqttClient;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // The MQTT client retries every 5s on its own. Only when that produces
            // neither a connection nor any message for 15 minutes the client is
            // likely wedged and a restart can actually help.
            if (!_mqttClient.IsConnected && _mqttClient.TimeSinceLastMessage > TimeSpan.FromMinutes(15))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"MQTT disconnected and silent for {_mqttClient.TimeSinceLastMessage.TotalMinutes:F1} minutes despite auto-reconnect"));
            }

            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}
