using InfluxDB3.Client;
using InfluxDB3.Client.Write;
using SharedContracts;
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

        public void WriteInfluxRecords(IEnumerable<InfluxRecord> records)
        {
            foreach (var record in records)
            {
                switch (record.MeasurementType)
                {
                    case MeasurementType.Percent:
                        WritePercentageValue(
                            (InfluxPercentageRecord)record,
                            DateTimeOffset.UtcNow);
                        break;
                    case MeasurementType.Energy:
                        WriteEnergyValue(
                            (InfluxEnergyRecord)record,
                            DateTimeOffset.UtcNow);
                        break;
                    case MeasurementType.Power:
                        WritePowerValue(
                            (InfluxPowerRecord)record,
                            DateTimeOffset.UtcNow);
                        break;
                    case MeasurementType.Voltage:
                        WriteVoltageValue(
                            (InfluxVoltageRecord)record,
                            DateTimeOffset.UtcNow);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
        public void WritePercentageValue(
            InfluxPercentageRecord record,
            DateTimeOffset timestamp)
        {
            var point = PointData.Measurement("percent_values")
                .SetTag("category", record.Category.ToString())
                .SetTag("sub_category", record.SubCategory.ToString())
                .SetTag("sensor_type", record.SensorType)
                .SetTag("location", record.Location)
                .SetTag("device", record.Device)
                .SetTag("measurement", record.Measurement)
                .SetField("value_percent", record.Value_Percent)
                .SetTimestamp(timestamp);
            client.WritePointAsync(point);
        }

        public void WriteEnergyValue(
            InfluxEnergyRecord record,
            DateTimeOffset timestamp)
        {
            var point = PointData.Measurement("energy_values")
                .SetTag("measurement_id", record.MeasurementId)
                .SetTag("category", record.Category.ToString())
                .SetTag("sub_category", record.SubCategory.ToString())
                .SetTag("sensor_type", record.SensorType)
                .SetTag("location", record.Location)
                .SetTag("device", record.Device)
                .SetTag("measurement", record.Measurement)
                .SetField("value_delta_kwh", Convert.ToDouble(record.Value_Delta_KWh))
                .SetField("value_cumulated_kwh", Convert.ToDouble(record.Value_Cumulated_KWh))
                .SetTimestamp(timestamp);
            client.WritePointAsync(point);
        }

        public void WritePowerValue(
            InfluxPowerRecord record,
            DateTimeOffset timestamp)
        {
            var point = PointData.Measurement("power_values")
                .SetTag("measurement_id", record.MeasurementId)
                .SetTag("category", record.Category.ToString())
                .SetTag("sub_category", record.SubCategory.ToString())
                .SetTag("sensor_type", record.SensorType)
                .SetTag("location", record.Location)
                .SetTag("device", record.Device)
                .SetTag("measurement", record.Measurement)
                .SetField("value_watt", Convert.ToDouble(record.Value_W))
                .SetTimestamp(timestamp);
            client.WritePointAsync(point);
        }

        public void WriteVoltageValue(
            InfluxVoltageRecord record,
            DateTimeOffset timestamp)
        {
            var point = PointData.Measurement("voltage_values")
                .SetTag("measurement_id", record.MeasurementId)
                .SetTag("category", record.Category.ToString())
                .SetTag("sub_category", record.SubCategory.ToString())
                .SetTag("sensor_type", record.SensorType)
                .SetTag("location", record.Location)
                .SetTag("device", record.Device)
                .SetTag("measurement", record.Measurement)
                .SetField("value_volt", Convert.ToDouble(record.Value_V))
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
