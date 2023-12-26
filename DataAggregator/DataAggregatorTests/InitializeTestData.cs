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

            //TODO: Make keys and connection strings secure
            var cosmosDBController = new CosmosDBController(
                   "https://smarthometsstorage.table.cosmos.azure.com:443/",
                   "UnitTestMinuteData",
                   "smarthometsstorage",
                   "yRZ84NCODris5jSJpP1tbZO1zxVkTTRSEsn4Yiu5TNyKFIToLOaMDe6whunduEzFT3tFwm95X4lcACDbRQDdPQ==");


            cosmosDBController.ClearTable();

            Dictionary<DateTime, double> testdata = new()
            {
                { new DateTime(2023, 12, 26, 10, 58, 45), 1.1 },
                { new DateTime(2023, 12, 26, 10, 59, 45), 2.2 },
                { new DateTime(2023, 12, 26, 11, 0, 45), 3.3 },
                { new DateTime(2023, 12, 26, 11, 1, 45), 4.4 },
                { new DateTime(2023, 12, 26, 11, 2, 45), 5.5 },
                { new DateTime(2023, 12, 26, 11, 3, 45), 6.6 },
                { new DateTime(2023, 12, 26, 11, 4, 45), 7.7 },
                { new DateTime(2023, 12, 26, 11, 5, 45), 8.8 },
                { new DateTime(2023, 12, 26, 11, 55, 45), 9.9 },
                { new DateTime(2023, 12, 26, 11, 56, 45), 10.10 },
                { new DateTime(2023, 12, 26, 11, 57, 45), 11.11 },
                { new DateTime(2023, 12, 26, 11, 58, 45), 12.12 },
                { new DateTime(2023, 12, 26, 11, 59, 40), 13.13 },
                { new DateTime(2023, 12, 26, 12, 0, 40), 14.14 },
                { new DateTime(2023, 12, 26, 12, 1, 40), 15.15 },
                { new DateTime(2023, 12, 26, 12, 2, 40), 16.16 },
                { new DateTime(2023, 12, 26, 12, 3, 40), 17.17 }
            };
            
            foreach (var item in testdata)
            {
                cosmosDBController.WriteData("sensor1", item.Key.ToString("yyyyMMddHHmmss"), item.Value, item.Key);
            }

            foreach (var item in testdata)
            {
                cosmosDBController.WriteData("sensor2", item.Key.ToString("yyyyMMddHHmmss"), item.Value*10, item.Key);
            }
        }
    }
}