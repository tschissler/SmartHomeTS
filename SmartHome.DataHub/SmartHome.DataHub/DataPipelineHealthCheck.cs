using Microsoft.Extensions.Diagnostics.HealthChecks;
using Influx3Connector;

namespace SmartHome.DataHub
{
    public class DataPipelineHealthCheck : IHealthCheck
    {
        private readonly MQTTClient.MQTTClient _mqttClient;
        private readonly InfluxDB3Connector _influxConnector;

        public DataPipelineHealthCheck(MQTTClient.MQTTClient mqttClient, InfluxDB3Connector influxConnector)
        {
            _mqttClient = mqttClient;
            _influxConnector = influxConnector;
        }

        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            // Check MQTT connectivity
            if (!_mqttClient.IsConnected)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("MQTT broker disconnected"));
            }
            
            // Check MQTT message reception
            if (_mqttClient.TimeSinceLastMessage > TimeSpan.FromMinutes(5))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"No MQTT messages received in {_mqttClient.TimeSinceLastMessage.TotalMinutes:F1} minutes"));
            }
            
            // Check InfluxDB writes
            if (_influxConnector.TimeSinceLastWrite > TimeSpan.FromMinutes(3))
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"No successful InfluxDB write in {_influxConnector.TimeSinceLastWrite.TotalMinutes:F1} minutes. Pending points: {_influxConnector.PendingPointsCount}"));
            }
            
            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}
