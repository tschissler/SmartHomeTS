using DataContracts;
using Newtonsoft.Json.Linq;
using StorageController;

namespace DataAggregatorTests
{
    [TestClass]
    public class InitializeTestData
    {
        [TestMethod]
        [TestCategory("ManualOnly")]
        public void InitializeMinuteTestData()
        {
            var cosmosDBController = new TableStorageController(
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri,
                   "UnitTestMinuteData",
                   "smarthomestorageprod",
                   SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey);

            cosmosDBController.ClearTable();

            Dictionary<DateTime, double> testdata = new()
            {
                { new DateTime(2023, 12, 28, 10, 58, 45, DateTimeKind.Utc), 1.1 },
                { new DateTime(2023, 12, 28, 10, 59, 45, DateTimeKind.Utc), 2.2 },
                { new DateTime(2023, 12, 28, 11,  0, 45, DateTimeKind.Utc), 3.3 },
                { new DateTime(2023, 12, 28, 11,  1, 45, DateTimeKind.Utc), 4.4 },
                { new DateTime(2023, 12, 28, 11,  2, 45, DateTimeKind.Utc), 5.5 },
                { new DateTime(2023, 12, 28, 11,  3, 45, DateTimeKind.Utc), 6.6 },
                { new DateTime(2023, 12, 28, 11,  4, 45, DateTimeKind.Utc), 7.7 },
                { new DateTime(2023, 12, 28, 11,  5, 45, DateTimeKind.Utc), 8.8 },
                { new DateTime(2023, 12, 28, 11, 55, 45, DateTimeKind.Utc), 9.9 },
                { new DateTime(2023, 12, 28, 11, 56, 45, DateTimeKind.Utc), 10.10 },
                { new DateTime(2023, 12, 28, 11, 57, 45, DateTimeKind.Utc), 11.11 },
                { new DateTime(2023, 12, 28, 11, 58, 45, DateTimeKind.Utc), 12.12 },
                { new DateTime(2023, 12, 28, 11, 59, 40, DateTimeKind.Utc), 13.13 },
                { new DateTime(2023, 12, 28, 12,  0, 40, DateTimeKind.Utc), 14.14 },
                { new DateTime(2023, 12, 28, 12,  1, 40, DateTimeKind.Utc), 15.15 },
                { new DateTime(2023, 12, 28, 12,  2, 40, DateTimeKind.Utc), 16.16 },
                { new DateTime(2023, 12, 28, 12,  3, 40, DateTimeKind.Utc), 17.17 },
                { new DateTime(2023, 12, 28, 18, 58, 40, DateTimeKind.Utc), 114.14 },
                { new DateTime(2023, 12, 28, 18, 59, 40, DateTimeKind.Utc), 115.15 },
                { new DateTime(2023, 12, 28, 19,  0, 40, DateTimeKind.Utc), 116.16 },
                { new DateTime(2023, 12, 28, 19,  1, 40, DateTimeKind.Utc), 117.17 },
                { new DateTime(2024, 3, 2, 8,  1, 40, DateTimeKind.Utc), 222.22 },
                { new DateTime(2024, 3, 3, 8,  1, 40, DateTimeKind.Utc), 333.33 }
            };
            
            foreach (var item in testdata)
            {
                cosmosDBController.WriteData("sensor1", Converters.ConvertDateTimeToReverseRowKey(item.Key), item.Value, item.Key);
            }

            foreach (var item in testdata)
            {
                cosmosDBController.WriteData("sensor2", Converters.ConvertDateTimeToReverseRowKey(item.Key), item.Value*10, item.Key);
            }
        }
    }
}