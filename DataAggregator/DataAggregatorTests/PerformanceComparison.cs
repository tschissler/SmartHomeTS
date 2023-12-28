using Azure.Data.Tables;
using DataContracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StorageController;
using System;
using System.Diagnostics;
using System.Reflection.Emit;

namespace DataAggregatorTests
{
    [Ignore]
    [TestClass]
    public class PerformanceComparison
    {
        private string storageUri = "https://smarthometsstorage.table.cosmos.azure.com:443/";
        private string storageAccountName = "smarthometsstorage";
        private string storageAccountKey = "yRZ84NCODris5jSJpP1tbZO1zxVkTTRSEsn4Yiu5TNyKFIToLOaMDe6whunduEzFT3tFwm95X4lcACDbRQDdPQ==";

        [TestMethod]
        public void MeassureLinqTakeAgainstQueryTake()
        {
            var tableName = "SmartHomeClimateRawData";
            var partitionKey = "1c50f3ab6224_humidity";
            var serviceClient = new TableServiceClient(
                new Uri(storageUri),
                new TableSharedKeyCredential(storageAccountName, storageAccountKey));
                        serviceClient.CreateTableIfNotExists(tableName);

            var tableClient = serviceClient.GetTableClient(tableName);

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            var query1 = tableClient.Query<DataValueTableEntity>()
                .Where(e => e.PartitionKey == partitionKey)
                .OrderByDescending(e => e.Time)
                .Take(100)
                .ToList();
            stopWatch.Stop();
            Console.WriteLine(stopWatch.ElapsedMilliseconds);
            stopWatch.Restart();
            var query2 = tableClient.Query<DataValueTableEntity>($"PartitionKey eq '{partitionKey}'", 100).Take(100).ToList();
            stopWatch.Stop();
            Console.WriteLine(stopWatch.ElapsedMilliseconds);
            stopWatch.Restart();
            var query3 = tableClient.Query<DataValueTableEntity>($"PartitionKey eq '{partitionKey}'", 100).Take(100).ToList();
            stopWatch.Stop();
            Console.WriteLine(stopWatch.ElapsedMilliseconds);
            stopWatch.Start();
            var query4 = tableClient.Query<DataValueTableEntity>()
                .Where(e => e.PartitionKey == partitionKey)
                .OrderByDescending(e => e.Time)
                .Take(100)
                .ToList();
            stopWatch.Stop();
            Console.WriteLine(stopWatch.ElapsedMilliseconds);
        }
    }
}