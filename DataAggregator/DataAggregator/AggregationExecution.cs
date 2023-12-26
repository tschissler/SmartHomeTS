using StorageController;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAggregator
{
    public class AggregationExecution
    {
        private static string storageUri = "https://smarthometsstorage.table.cosmos.azure.com:443/";
        private static string storageAccountName = "smarthometsstorage";
        private static string storageAccountKey = "yRZ84NCODris5jSJpP1tbZO1zxVkTTRSEsn4Yiu5TNyKFIToLOaMDe6whunduEzFT3tFwm95X4lcACDbRQDdPQ==";

        public static void AggregateTemperatureHourly(string partitionKey, string minuteTableName, string hourTableName)
        {
            var minuteCosmosDBConnection = new CosmosDBController(storageUri, minuteTableName, storageAccountName, storageAccountKey);
            var hourCosmosDBConnection = new CosmosDBController(storageUri, hourTableName, storageAccountName, storageAccountKey);

            DateTime lastHour = DateTime.MinValue;
            var lastitem = hourCosmosDBConnection.ReadTop1Data($"PartitionKey eq '{partitionKey}'");
            if (lastitem != null)
            {
                lastHour = lastitem.LocalTime;
            }
            var data = AggregationCalculation.GetValueAtTopOfEveryHour(minuteTableName, partitionKey, lastHour).Result;

            foreach ( var item in data )
            {
                hourCosmosDBConnection.WriteData(partitionKey, item.RowKey, item.Value, item.Time);
            }
        }
    }
}
