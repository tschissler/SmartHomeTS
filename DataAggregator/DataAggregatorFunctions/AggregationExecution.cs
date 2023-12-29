using Microsoft.Extensions.Logging;
using StorageController;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAggregatorFunctions
{
    public class AggregationExecution
    {
        private static string storageUri = "https://smarthometsstorage.table.cosmos.azure.com:443/";
        private static string storageAccountName = "smarthometsstorage";
        private static string storageAccountKey = "yRZ84NCODris5jSJpP1tbZO1zxVkTTRSEsn4Yiu5TNyKFIToLOaMDe6whunduEzFT3tFwm95X4lcACDbRQDdPQ==";

        public static int AggregateClimateHourlyData(string partitionKey, string minuteTableName, string hourTableName, DateTime maxTime = new DateTime())
        {
            var hourCosmosDBConnection = new CosmosDBController(storageUri, hourTableName, storageAccountName, storageAccountKey);

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

        public static void AggregateClimateData(ILogger logger)
        {
            List<string> partitionKeys = new()
            {
                "1c50f3ab6224_temperature",
                "44dbf3ab6224_temperature",
                "a86d2b286f24_temperature",
                "1420381fb608_temperature",
                "1c50f3ab6224_humidity",
                "44dbf3ab6224_humidity",
                "a86d2b286f24_humidity",
                "1420381fb608_humidity"
            };

            foreach (var partitionKey in partitionKeys)
            {
                var count = AggregationExecution.AggregateClimateHourlyData(partitionKey, "SmartHomeClimateRawData", "SmartHomeClimateHourAggregationData");
                logger.LogInformation($"Aggregated {count} items for {partitionKey}");
            }
        }
    }
}
