using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KebaConnector
{
    public class KebaConnectorHealthCheck : IHealthCheck
    {
        private static DateTime _lastSuccessfulRead = DateTime.MinValue;
        private static bool _isMqttConnected = false;

        /// <summary>
        /// When a wallbox was last read successfully; null before the first read. Exposed so the
        /// service heartbeat on status/ reports the same moment this check judges by - two
        /// counters would let readiness and the device page disagree.
        /// </summary>
        public static DateTimeOffset? LastSuccessfulRead => _lastSuccessfulRead == DateTime.MinValue
            ? null
            : new DateTimeOffset(_lastSuccessfulRead, TimeSpan.Zero);

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
                // Not "starting up": this branch is also taken after three days, and then that
                // word tells a reader the wrong story - "give it a moment" instead of "this has
                // never worked". The state is genuinely healthy (MQTT is connected), so the
                // message states the fact instead of claiming a phase.
                    ? "No successful read yet"
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
