using DataAggregator;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using StorageController;
using System;

namespace DataAggregatorTests
{
    [TestClass]
    public class AggregateTemperatureHourTests
    {
        private const string storageUri = "https://smarthometsstorage.table.cosmos.azure.com:443/";
        private const string storageAccountName = "smarthometsstorage";
        private const string storageAccountKey = "yRZ84NCODris5jSJpP1tbZO1zxVkTTRSEsn4Yiu5TNyKFIToLOaMDe6whunduEzFT3tFwm95X4lcACDbRQDdPQ==";
        private const string minuteTableName = "UnitTestMinuteData";
        private const string hourTableName = "UnitTestHourData";

        [TestMethod]
        public void AggregateAll()
        {
            var minuteCosmosDBConnection = new CosmosDBController(storageUri, minuteTableName, storageAccountName, storageAccountKey);
            var hourCosmosDBConnection = new CosmosDBController(storageUri, hourTableName, storageAccountName, storageAccountKey);

            hourCosmosDBConnection.ClearTable();
            AggregationExecution.AggregateTemperatureHourly("sensor1", minuteTableName, hourTableName);
            var result = hourCosmosDBConnection.ReadData("PartitionKey eq 'sensor1'");
            result.Should().HaveCount(3);
            result[0].Value.Should().Be(1.1);
            result[1].Value.Should().Be(3.3);
            result[2].Value.Should().Be(14.14);
        }

        [TestMethod]
        public void AggregateProductionData()
        {
            List<string> partitionKeys = new()
            {
                "1c50f3ab6224_temperature",
                "44dbf3ab6224_temperature",
                "a86d2b286f24_temperature",
                "1420381fb608_temperature"
            };

            foreach (var partitionKey in partitionKeys)
            {
                AggregationExecution.AggregateTemperatureHourly(partitionKey, "SmartHomeData", "SmartHomeHourAggregationData");
            }
        }

        [TestMethod]
        public void AggregateSome()
        {
            var minuteCosmosDBConnection = new CosmosDBController(storageUri, minuteTableName, storageAccountName, storageAccountKey);
            var hourCosmosDBConnection = new CosmosDBController(storageUri, hourTableName, storageAccountName, storageAccountKey);

            hourCosmosDBConnection.ClearTable();
            hourCosmosDBConnection.WriteData("sensor1", "20231226110045", 3.3, new DateTime(2023,12,26,11,00,45));
            AggregationExecution.AggregateTemperatureHourly("sensor1", minuteTableName, hourTableName);

            var result = hourCosmosDBConnection.ReadData("PartitionKey eq 'sensor1'");
            result.Should().HaveCount(2);
            result[0].Value.Should().Be(3.3);
            result[1].Value.Should().Be(14.14);

        }
    }
}