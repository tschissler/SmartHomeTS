using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KebaConnector
{
    public class KebaConnectorHealthCheck : IHealthCheck
    {
        private static DateTime _lastSuccessfulRead = DateTime.MinValue;
        private static bool _isMqttConnected = false;

        public static void UpdateLastSuccessfulRead()
        {
            _lastSuccessfulRead = DateTime.UtcNow;
        }

        public static void UpdateMqttConnectionStatus(bool isConnected)
        {
            _isMqttConnected = isConnected;
        }

        public Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            // Check if we've successfully read data within the last 5 minutes
            var timeSinceLastRead = DateTime.UtcNow - _lastSuccessfulRead;
            var isHealthy = _isMqttConnected &&
                           (_lastSuccessfulRead == DateTime.MinValue || timeSinceLastRead.TotalMinutes < 5);

            if (isHealthy)
            {
                var message = _lastSuccessfulRead == DateTime.MinValue
                    ? "Service is starting up"
                    : $"Last successful read: {timeSinceLastRead.TotalSeconds:F0} seconds ago";

                return Task.FromResult(
                    HealthCheckResult.Healthy(message));
            }

            var unhealthyMessage = !_isMqttConnected
                ? "MQTT not connected"
                : $"No successful data read for {timeSinceLastRead.TotalMinutes:F1} minutes";

            return Task.FromResult(
                HealthCheckResult.Unhealthy(unhealthyMessage));
        }
    }
}
