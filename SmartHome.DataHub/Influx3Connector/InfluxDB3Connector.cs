using InfluxDB3.Client;
using InfluxDB3.Client.Write;
using Microsoft.Extensions.Logging;
using SharedContracts;
using System.Diagnostics.Metrics;

namespace Influx3Connector
{
    public class InfluxDB3Connector : IDisposable
    {
        private InfluxDBClient client;
        private readonly string influxUrl;
        private readonly string database;
        private readonly string token;
        private readonly List<PointData> pointsBatch = new List<PointData>();
        private const int batchIntervalSeconds = 5;
        private DateTimeOffset nextBatchTime = DateTimeOffset.UtcNow.AddSeconds(batchIntervalSeconds); // Define batch interval
        private readonly ILogger logger;
        private DateTimeOffset lastSuccessfulWrite = DateTimeOffset.UtcNow;
        private bool isDisposed = false;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();


        public InfluxDB3Connector(string influxUrl, string database, string token, ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.influxUrl = influxUrl;
            this.database = database;
            this.token = token;
            client = new InfluxDBClient(influxUrl, token: token, database: database);
            Task.Run(async () => await ProcessBatchAsync(cancellationTokenSource.Token));
            Task.Run(async () => await HealthCheckAsync(cancellationTokenSource.Token));
        }
        
        /// <summary>
        /// Returns the time since the last successful write to InfluxDB
        /// </summary>
        public TimeSpan TimeSinceLastWrite => DateTimeOffset.UtcNow - lastSuccessfulWrite;
        
        /// <summary>
        /// Returns the current number of pending points in the batch
        /// </summary>
        public int PendingPointsCount
        {
            get
            {
                lock (pointsBatch)
                {
                    return pointsBatch.Count;
                }
            }
        }
        
        private async Task HealthCheckAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                
                if (TimeSinceLastWrite > TimeSpan.FromMinutes(10))
                {
                    logger.LogWarning($"InfluxDB3Connector: No successful write in {TimeSinceLastWrite.TotalMinutes:F1} minutes. Pending points: {PendingPointsCount}");
                }
                else
                {
                    logger.LogInformation($"InfluxDB3Connector: Health OK. Last write: {TimeSinceLastWrite.TotalSeconds:F0}s ago. Pending: {PendingPointsCount}");
                }
            }
        }
        
        private void RecreateClient()
        {
            try
            {
                client?.Dispose();
            }
            catch (Exception ex)
            {
                logger.LogWarning($"InfluxDB3Connector: Error disposing old client: {ex.Message}");
            }
            
            client = new InfluxDBClient(influxUrl, token: token, database: database);
            logger.LogInformation("InfluxDB3Connector: Client recreated.");
        }
        
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            
            cancellationTokenSource.Cancel();
            
            try
            {
                // Try to flush remaining data
                FlushBatchAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.LogWarning($"InfluxDB3Connector: Error flushing on dispose: {ex.Message}");
            }
            
            client?.Dispose();
            cancellationTokenSource.Dispose();
        }

        public void WriteInfluxRecords(IEnumerable<InfluxRecord> records)
        {
            foreach (var record in records)
            {
                switch (record.MeasurementType)
                {
                    case MeasurementType.Temperature:
                        WriteTemperatureValue(
                            (InfluxTemperatureRecord)record,
                            DateTimeOffset.UtcNow);
                        break;
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
                .SetTag("measurement_id", record.MeasurementId)
                .SetTag("category", record.Category.ToString())
                .SetTag("sub_category", record.SubCategory.ToString())
                .SetTag("sensor_type", record.SensorType)
                .SetTag("location", record.Location)
                .SetTag("device", record.Device)
                .SetTag("measurement", record.Measurement)
                .SetField("value_percent", record.Value_Percent)
                .SetTimestamp(timestamp);
            lock (pointsBatch)
            {
                pointsBatch.Add(point);
            }
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
            lock (pointsBatch)
            {
                pointsBatch.Add(point);
            }
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
            lock (pointsBatch)
            {
                pointsBatch.Add(point);
            }
        }

        public void WriteTemperatureValue(
            InfluxTemperatureRecord record,
            DateTimeOffset timestamp)
        {
            var point = PointData.Measurement("temperature_values")
                .SetTag("measurement_id", record.MeasurementId)
                .SetTag("category", record.Category.ToString())
                .SetTag("sub_category", record.SubCategory)
                .SetTag("sensor_type", record.SensorType)
                .SetTag("location", record.Location)
                .SetTag("device", record.Device)
                .SetTag("measurement", record.Measurement)
                .SetField("value_temp", Convert.ToDouble(record.Value_DegreeC))
                .SetTimestamp(timestamp);
            lock (pointsBatch)
            {
                pointsBatch.Add(point);
            }
        }

        public void WriteStatusValue(
            InfluxStatusRecord record,
            DateTimeOffset timestamp)
        {
            var point = PointData.Measurement("status_values")
                .SetTag("measurement_id", record.MeasurementId)
                .SetTag("category", record.Category.ToString())
                .SetTag("sub_category", record.SubCategory)
                .SetTag("sensor_type", record.SensorType)
                .SetTag("location", record.Location)
                .SetTag("device", record.Device)
                .SetTag("measurement", record.Measurement)
                .SetField("value_status", Convert.ToInt16(record.Value_Status))
                .SetTimestamp(timestamp);
            lock (pointsBatch)
            {
                pointsBatch.Add(point);
            }
        }

        public void WriteCounterValue(
            InfluxCounterRecord record,
            DateTimeOffset timestamp)
        {
            var point = PointData.Measurement("counter_values")
                .SetTag("measurement_id", record.MeasurementId)
                .SetTag("category", record.Category.ToString())
                .SetTag("sub_category", record.SubCategory)
                .SetTag("sensor_type", record.SensorType)
                .SetTag("location", record.Location)
                .SetTag("device", record.Device)
                .SetTag("measurement", record.Measurement)
                .SetField("value_counter", Convert.ToInt16(record.Value_Counter))
                .SetTimestamp(timestamp);
            lock (pointsBatch)
            {
                pointsBatch.Add(point);
            }
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
            lock (pointsBatch)
            {
                pointsBatch.Add(point);
            }
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

            lock (pointsBatch)
            {
                pointsBatch.Add(point);
            }
        }

        private async Task FlushBatchAsync()
        {
            List<PointData> batchCopy;
            lock (pointsBatch)
            {
                batchCopy = new List<PointData>(pointsBatch);
                pointsBatch.Clear();
            }

            if (batchCopy.Count > 0)
            {
                try
                {
                    await client.WritePointsAsync(batchCopy);
                }
                catch (Exception ex)
                {
                    logger.LogError($"InfluxDB3Connector: Error writing batch to InfluxDB: {ex.Message}");
                    // Re-add failed points to batch for retry (optional - consider max retry limit)
                    lock (pointsBatch)
                    {
                        pointsBatch.InsertRange(0, batchCopy);
                        // Prevent unbounded growth - limit to last 10000 points
                        if (pointsBatch.Count > 10000)
                        {
                            var droppedCount = pointsBatch.Count - 10000;
                            pointsBatch.RemoveRange(0, droppedCount);
                            logger.LogWarning($"InfluxDB3Connector: Dropped {droppedCount} oldest points due to buffer overflow.");
                        }
                    }
                    throw; // Re-throw to be handled by ProcessBatchAsync
                }
            }
        }

        private async Task ProcessBatchAsync(CancellationToken cancellationToken)
        {
            int consecutiveFailures = 0;
            const int maxConsecutiveFailures = 10;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, cancellationToken); // Process every second
                    if (DateTimeOffset.UtcNow >= nextBatchTime)
                    {
                        await FlushBatchAsync();
                        nextBatchTime = DateTimeOffset.UtcNow.AddSeconds(batchIntervalSeconds); // Reset timer
                        lastSuccessfulWrite = DateTimeOffset.UtcNow;
                        logger.LogInformation("InfluxDB3Connector: Flushed batch to InfluxDB.");
                        consecutiveFailures = 0; // Reset on success
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation("InfluxDB3Connector: ProcessBatchAsync cancelled.");
                    break;
                }
                catch (Exception ex)
                {
                    consecutiveFailures++;
                    logger.LogError($"InfluxDB3Connector: ProcessBatchAsync error (attempt {consecutiveFailures}/{maxConsecutiveFailures}): {ex.Message}");
                    
                    if (consecutiveFailures >= maxConsecutiveFailures)
                    {
                        logger.LogCritical("InfluxDB3Connector: Max consecutive failures reached. Attempting to recreate client...");
                        RecreateClient();
                        
                        // Wait longer before retry after client recreation
                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                        consecutiveFailures = 0;
                    }
                    else
                    {
                        // Exponential backoff
                        await Task.Delay(TimeSpan.FromSeconds(Math.Min(consecutiveFailures * 2, 30)), cancellationToken);
                    }
                }
            }
        }
    }
}
