using DataAggregatorFunctions;
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
            AggregationExecution.AggregateClimateHourlyData("sensor1", minuteTableName, hourTableName);
            var result = hourCosmosDBConnection.ReadData("PartitionKey eq 'sensor1'");
            result.Should().HaveCount(3);
            result[0].Value.Should().Be(1.1);
            result[1].Value.Should().Be(3.3);
            result[2].Value.Should().Be(14.14);
        }

        [TestMethod]
        public void AggregateValuesAfter11UTCTime()
        {
            var hourCosmosDBConnection = new CosmosDBController(storageUri, hourTableName, storageAccountName, storageAccountKey);

            hourCosmosDBConnection.ClearTable();
            var lastDataDate = new DateTime(2023, 12, 28, 11, 00, 45, DateTimeKind.Utc);
            hourCosmosDBConnection.WriteData("sensor1", Converters.ConvertDateTimeToReverseRowKey(lastDataDate), 3.3, lastDataDate);
            AggregationExecution.AggregateClimateHourlyData("sensor1", minuteTableName, hourTableName, new DateTime(2023, 12, 28, 12, 30, 0, DateTimeKind.Utc));

            var result = hourCosmosDBConnection.ReadData("PartitionKey eq 'sensor1'");
            result.Should().HaveCount(2);
            result[0].Value.Should().Be(3.3);
            result[1].Value.Should().Be(14.14);

            // Ensure that the data is not aggregated twice
            AggregationExecution.AggregateClimateHourlyData("sensor1", minuteTableName, hourTableName, new DateTime(2023, 12, 28, 12, 30, 0, DateTimeKind.Utc));
            result = hourCosmosDBConnection.ReadData("PartitionKey eq 'sensor1'");
            result.Should().HaveCount(2);
            result[0].Value.Should().Be(3.3);
            result[1].Value.Should().Be(14.14);
        }
    }
}