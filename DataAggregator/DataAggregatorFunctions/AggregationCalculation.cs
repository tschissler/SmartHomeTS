using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataContracts;
using StorageController;

namespace DataAggregatorFunctions
{
    public class AggregationCalculation
    {
        private static string storageAccountName = "smarthomestorageprod";

        public static async Task<List<DataValueTableEntity>> GetValueAtTopOfEveryHour(string tableName, string partitionKey, DateTime startTime, DateTime maxTime = new DateTime())
        {
            var result = new List<DataValueTableEntity>();
            DateTime topOfHour = CalculateTopOfTheHour(startTime);
            if (maxTime == DateTime.MinValue )
            {
                maxTime = DateTime.UtcNow;
            }
            while (topOfHour < maxTime)
            {
                string filter = $"PartitionKey eq '{partitionKey}' " +
                    $"and Time gt datetime'{startTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}' " +
                    $"and Time le datetime'{topOfHour.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}'";
                var data = new CosmosDBController(
                    SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri,
                    tableName, 
                    storageAccountName, 
                    SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey).ReadTop1Data(filter);

                if (data != null)
                {
                    result.Add(data);
                    topOfHour = CalculateTopOfTheHour(data.Time);
                    startTime = data.Time;
                }
                else
                {
                    topOfHour = topOfHour.AddHours(1);
                }
            }
            return result;
        }

        public static DateTime CalculateTopOfTheHour(DateTime startTime)
        {
            return new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, 0, 0, startTime.Kind).AddHours(1);
        }
    }
}
