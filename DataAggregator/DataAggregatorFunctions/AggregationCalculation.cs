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
                DataValueTableEntity data = GetDataWithinTimeFrame(tableName, partitionKey, startTime, topOfHour);

                if (data != null)
                {
                    result.Add(data);
                    topOfHour = CalculateTopOfTheHour(data.Time);
                    startTime = data.Time;
                }
                else
                {
                    // Try to find data within the next day
                    data = GetDataWithinTimeFrame(tableName, partitionKey, startTime, topOfHour.AddDays(1));
                    if (data != null)
                    {
                        topOfHour = topOfHour.AddHours(1);
                    }
                    else
                    {
                        // Try to find data within the next 30 days 
                        data = GetDataWithinTimeFrame(tableName, partitionKey, startTime, topOfHour.AddDays(30));
                        if (data != null)
                        {
                            topOfHour = topOfHour.AddDays(1);
                        }
                        else
                        {
                            topOfHour = topOfHour.AddDays(30);
                        }
                    }
                }
            }
            return result;
        }

        private static DataValueTableEntity GetDataWithinTimeFrame(string tableName, string partitionKey, DateTime startTime, DateTime endTime)
        {
            string filter = $"PartitionKey eq '{partitionKey}' " +
                $"and Time gt datetime'{startTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}' " +
                $"and Time le datetime'{endTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ")}'";
            var data = new TableStorageController(
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri,
                tableName,
                storageAccountName,
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey).ReadTop1Data(filter);
            return data;
        }

        private static IEnumerable<DataValueTableEntity> GetValuesAtDay(string tableName, string partitionKey, DateOnly day)
        {
            string filter = $"PartitionKey eq '{partitionKey}' " +
                $"and Time ge datetime'{day.ToString("yyyy-MM-dd")}' " +
                $"and Time lt datetime'{day.AddDays(1).ToString("yyyy-MM-dd")}'";
            var data = new TableStorageController(
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri,
                tableName,
                storageAccountName,
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey).ReadData(filter);
            return data;
        }

        public static DateTime CalculateTopOfTheHour(DateTime startTime)
        {
            return new DateTime(startTime.Year, startTime.Month, startTime.Day, startTime.Hour, 0, 0, startTime.Kind).AddHours(1);
        }

        public static async Task<List<DataValueTableEntity>> GetValuesPerDay(string hourTableName, string partitionKey, DateOnly lastDay, DateTime maxDate)
        {
            var result = new List<DataValueTableEntity>();
            DateOnly day = lastDay;
            if (maxDate == DateTime.MinValue)
            {
                maxDate = DateTime.UtcNow;
            }
            while (day < DateOnly.FromDateTime(maxDate))
            {
                var data = GetValuesAtDay(hourTableName, partitionKey, day);

                if (data != null && data.Count() > 0)
                {
                    result.Add(new DataValueTableEntity
                    {
                        PartitionKey = partitionKey,
                        RowKey = data.Min(d => d.RowKey),
                        MaxValue = data.Max(d => d.Value),
                        MinValue = data.Min(d => d.Value),
                        Value = data.Average(d => d.Value),
                        Time = data.First().Time.Date,
                    });
                    day = day.AddDays(1);
                }
                else
                {
                    // Try to find data within the next 30 days
                    var nextData = GetDataWithinTimeFrame(hourTableName, partitionKey, day.ToDateTime(TimeOnly.MinValue), day.AddDays(30).ToDateTime(TimeOnly.MinValue));
                    if (nextData != null)
                    {
                        day = day.AddDays(1);
                    }
                    else
                    {
                        day = day.AddDays(30);
                    }
                }
            }
            return result;
        }
    }
}
