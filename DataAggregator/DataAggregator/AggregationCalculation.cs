using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataContracts;
using StorageController;

namespace DataAggregator
{
    public class AggregationCalculation
    {
        private static string storageUri = "https://smarthometsstorage.table.cosmos.azure.com:443/";
        private static string storageAccountName = "smarthometsstorage";
        private static string storageAccountKey = "yRZ84NCODris5jSJpP1tbZO1zxVkTTRSEsn4Yiu5TNyKFIToLOaMDe6whunduEzFT3tFwm95X4lcACDbRQDdPQ==";

        public static async Task<List<DataValueTableEntity>> GetValueAtTopOfEveryHour(string tableName, string partitionKey, DateTime startTime, DateTime maxTime = new DateTime())
        {
            var result = new List<DataValueTableEntity>();
            DateTime topOfHour = CalculateTopOfTheHour(startTime);
            if (maxTime == DateTime.MinValue )
            {
                maxTime = DateTime.Now;
            }
            while (topOfHour < maxTime.AddHours(1))
            {
                string filter = $"PartitionKey eq '{partitionKey}' " +
                    $"and Time ge datetime'{topOfHour.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}' " +
                    $"and Time lt datetime'{topOfHour.AddHours(1).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}'";
                var data = new CosmosDBController(storageUri, tableName, storageAccountName, storageAccountKey).ReadTop1Data(filter);

                if (data != null)
                {
                    result.Add(data);
                }
                topOfHour = topOfHour.AddHours(1);
            }
            return result;
        }

        public static DateTime CalculateTopOfTheHour(DateTime startTime)
        {
            return new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, 0, 0, startTime.Kind).AddHours(1);
        }
    }
}
