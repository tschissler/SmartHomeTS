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
        private string storageUri = SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri; 
        private string storageAccountKey = SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey;
        private const string storageAccountName = "smarthomestorageprod";
        private const string minuteTableName = "UnitTestMinuteData";
        private const string hourTableName = "UnitTestHourData";
        private const string dayTableName = "UnitTestDayData";

        [TestMethod]
        public void AggregateHourlyData()
        {
            var minuteCosmosDBConnection = new TableStorageController(storageUri, minuteTableName, storageAccountName, storageAccountKey);
            var hourCosmosDBConnection = new TableStorageController(storageUri, hourTableName, storageAccountName, storageAccountKey);

            hourCosmosDBConnection.ClearTable();
            AggregationExecution.AggregateClimateHourlyData("sensor1", minuteTableName, hourTableName);
            var result = hourCosmosDBConnection.ReadData("PartitionKey eq 'sensor1'");
            result.Should().HaveCount(7);
            result[0].Value.Should().Be(333.33);
            result[1].Value.Should().Be(222.22);
            result[2].Value.Should().Be(117.17);
            result[3].Value.Should().Be(115.15);
            result[4].Value.Should().Be(17.17);
            result[5].Value.Should().Be(13.13);
            result[6].Value.Should().Be(2.2);
        }

        [TestMethod]
        public void AggregateValuesAfter11UTCTime()
        {
            var hourCosmosDBConnection = new TableStorageController(storageUri, hourTableName, storageAccountName, storageAccountKey);

            hourCosmosDBConnection.ClearTable();
            var lastDataDate = new DateTime(2023, 12, 28, 11, 10, 45, DateTimeKind.Utc);
            hourCosmosDBConnection.WriteData("sensor1", Converters.ConvertDateTimeToReverseRowKey(lastDataDate), 3.3, lastDataDate);
            AggregationExecution.AggregateClimateHourlyData("sensor1", minuteTableName, hourTableName, new DateTime(2023, 12, 28, 19, 30, 0, DateTimeKind.Utc));

            var result = hourCosmosDBConnection.ReadData("PartitionKey eq 'sensor1'");
            result.Should().HaveCount(4);
            result[0].Value.Should().Be(115.15);
            result[1].Value.Should().Be(17.17);
            result[2].Value.Should().Be(13.13);
            //Data from initialization
            result[3].Value.Should().Be(3.3);

            // Ensure that the data is not aggregated twice
            AggregationExecution.AggregateClimateHourlyData("sensor1", minuteTableName, hourTableName, new DateTime(2023, 12, 28, 19, 30, 0, DateTimeKind.Utc));
            result = hourCosmosDBConnection.ReadData("PartitionKey eq 'sensor1'");
            result.Should().HaveCount(4);
            result[0].Value.Should().Be(115.15);
            result[1].Value.Should().Be(17.17);
            result[2].Value.Should().Be(13.13);
            //Data from initialization
            result[3].Value.Should().Be(3.3);
        }

        [TestMethod]
        public void AggregateDailyData()
        {
            var dayDBConnection = new TableStorageController(storageUri, dayTableName, storageAccountName, storageAccountKey);

            dayDBConnection.ClearTable();
            AggregationExecution.AggregateClimateDailyData("sensor1", hourTableName, dayTableName, new DateTime(2024, 3, 3, 8, 0, 0));
            var result = dayDBConnection.ReadData("PartitionKey eq 'sensor1'");
            result.Should().HaveCount(2);
            result[0].Value.Should().Be(222.22);
            result[0].MinValue.Should().Be(222.22);
            result[0].MaxValue.Should().Be(222.22);
            result[0].Time.Should().Be(new DateTime(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc));
            result[1].Value.Should().Be(52.964);
            result[1].MinValue.Should().Be(2.2);
            result[1].MaxValue.Should().Be(117.17);
            result[1].Time.Should().Be(new DateTime(2023, 12, 28, 0, 0, 0, DateTimeKind.Utc));

            // Ensure that the data is not aggregated twice
            AggregationExecution.AggregateClimateDailyData("sensor1", hourTableName, dayTableName, new DateTime(2024, 3, 3, 8, 0, 0));
            result = dayDBConnection.ReadData("PartitionKey eq 'sensor1'");
            result.Should().HaveCount(2);
            result[0].Value.Should().Be(222.22);
            result[0].MinValue.Should().Be(222.22);
            result[0].MaxValue.Should().Be(222.22);
            result[0].Time.Should().Be(new DateTime(2024, 3, 2, 0, 0, 0, DateTimeKind.Utc));
            result[1].Value.Should().Be(52.964);
            result[1].MinValue.Should().Be(2.2);
            result[1].MaxValue.Should().Be(117.17);
            result[1].Time.Should().Be(new DateTime(2023, 12, 28, 0, 0, 0, DateTimeKind.Utc));
        }
    }
}