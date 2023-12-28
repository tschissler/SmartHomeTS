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

        public static void AggregateTemperatureHourly(string partitionKey, string minuteTableName, string hourTableName, DateTime maxTime = new DateTime())
        {
            var hourCosmosDBConnection = new CosmosDBController(storageUri, hourTableName, storageAccountName, storageAccountKey);

            DateTime lastHour = new DateTime(2023, 12, 28, 0, 0, 0);
            //var lastitem = hourCosmosDBConnection.ReadTop1Data($"PartitionKey eq '{partitionKey}'");
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
        }
    }
}
