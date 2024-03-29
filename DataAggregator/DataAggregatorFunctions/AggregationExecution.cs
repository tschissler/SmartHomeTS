using Microsoft.Extensions.Logging;
using StorageController;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAggregatorFunctions
{
    public class AggregationExecution
    {
        private static string storageAccountName = "smarthomestorageprod";

        public static void AggregateClimateData(ILogger logger, string rawDataTableName, string hourlyDataTableName, string dailyDataTableName)
        {
            List<string> partitionKeys = new()
            {
                "1c50f3ab6224_temperature",
                "1420381fb608_temperature",
                "88ff1305613c_temperature",
                "a86d2b286f24_temperature",
                "1c50f3ab6224_humidity",
                "1420381fb608_humidity",
                "88ff1305613c_humidity"
            };

            foreach (var partitionKey in partitionKeys)
            {
                var count = AggregationExecution.AggregateClimateHourlyData(partitionKey, rawDataTableName, hourlyDataTableName);
                if (logger != null)
                {
                    logger.LogInformation($"Aggregated {count} items for {partitionKey} hourly data");
                }

                count = AggregationExecution.AggregateClimateDailyData(partitionKey, hourlyDataTableName, dailyDataTableName);
                if (logger != null)
                {
                    logger.LogInformation($"Aggregated {count} items for {partitionKey} daily data");
                }
            }
        }

        public static int AggregateClimateHourlyData(string partitionKey, string minuteTableName, string hourTableName, DateTime maxTime = new DateTime())
        {
            var hourCosmosDBConnection = new TableStorageController(
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri,
                hourTableName, 
                storageAccountName, 
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey);

            DateTime lastHour = new DateTime(2023, 12, 28, 0, 0, 0);
            var lastitem = hourCosmosDBConnection.GetNewestItem(partitionKey);
            if (lastitem != null)
            {
                lastHour = lastitem.Time;
            }
            var data = AggregationCalculation.GetValueAtTopOfEveryHour(minuteTableName, partitionKey, lastHour, maxTime).Result;

            foreach ( var item in data )
            {
                hourCosmosDBConnection.WriteData(partitionKey, item.RowKey, item.Value, item.Time);
            }

            return data.Count;
        }

        public static int AggregateClimateDailyData(string partitionKey, string hourTableName, string dayTableName, DateTime maxTime = new DateTime())
        {
            var dayDBConnection = new TableStorageController(
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri,
                dayTableName,
                storageAccountName,
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey);

            var lastDay = new DateOnly(2023, 12, 28);
            var lastitem = dayDBConnection.GetNewestItem(partitionKey);
            if (lastitem != null)
            {
                lastDay = DateOnly.FromDateTime(lastitem.Time).AddDays(1);
            }
            var data = AggregationCalculation.GetValuesPerDay(hourTableName, partitionKey, lastDay, maxTime).Result;

            foreach (var item in data)
            {
                dayDBConnection.WriteData(item);
            }

            return data.Count;
        }
    }
}
