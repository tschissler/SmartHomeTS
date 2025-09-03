using InfluxDB3.Client;
using InfluxDB3.Client.Write;
using System.Diagnostics.Metrics;

namespace Influx3Connector
{
    public class InfluxDB3Connector
    {
        private InfluxDBClient client;

        public InfluxDB3Connector(string influxUrl, string database, string token)
        {
            client = new InfluxDBClient(influxUrl, token: token, database: database);
        }

        public void WritePercentageValue(
            string category,
            string sensorType,
            string location,
            string device,
            string meassurement,
            double value_percent,
            DateTimeOffset timestamp)
        {
            var point = PointData.Measurement("percent_values")
                .SetTag("category", category)
                .SetTag("sensor_type", sensorType)
                .SetTag("location", location)
                .SetTag("device", device)
                .SetTag("meassurement", meassurement)
                .SetField("value_percent", value_percent)
                .SetTimestamp(timestamp);
            client.WritePointAsync(point);
        }

        public void WriteEnergyValue(
            string category,
            string sensorType,
            string location,
            string device,
            string meassurement,
            double value_kwh,
            DateTimeOffset timestamp)
        {
            var point = PointData.Measurement("energy_values")
                .SetTag("category", category)
                .SetTag("sensor_type", sensorType)
                .SetTag("location", location)
                .SetTag("device", device)
                .SetTag("meassurement", meassurement)
                .SetField("value_kwh", value_kwh)
                .SetTimestamp(timestamp);
            client.WritePointAsync(point);
        }

        public void WritePowerValue(
            string category,
            string sensorType,
            string location,
            string device,
            string meassurement,
            double value_watt,
            DateTimeOffset timestamp)
        {
            var point = PointData.Measurement("power_values")
                .SetTag("category", category)
                .SetTag("sensor_type", sensorType)
                .SetTag("location", location)
                .SetTag("device", device)
                .SetTag("meassurement", meassurement)
                .SetField("value_watt", value_watt)
                .SetTimestamp(timestamp);
            client.WritePointAsync(point);
        }

        public void WritePointDataToInfluxDb(
            string measurement,
            IEnumerable<(string Key, string Value)> tags,
            IEnumerable<(string Key, object Value)> fields,
            DateTimeOffset timestamp)
        {
            var point = PointData.Measurement(measurement)
                .SetTimestamp(timestamp);
            
            foreach (var tag in tags)
            {
                point.SetTag(tag.Key, tag.Value);
            }
            
            foreach (var field in fields)
            {
                point.SetField(field.Key, field.Value);
            }

            client.WritePointAsync(point);
        }
    }
}
