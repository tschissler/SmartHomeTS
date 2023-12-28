using Microsoft.VisualStudio.TestTools.UnitTesting;
using StorageConnector;
using StorageController;
using System;

namespace StorageConnectorTests
{
    [TestClass]
    public class RecalculateRowKeysTests
    {
        private string storageUri = "https://smarthometsstorage.table.cosmos.azure.com:443/";
        private string storageAccountName = "smarthometsstorage";
        private string storageAccountKey = "yRZ84NCODris5jSJpP1tbZO1zxVkTTRSEsn4Yiu5TNyKFIToLOaMDe6whunduEzFT3tFwm95X4lcACDbRQDdPQ==";
        private string tableName = "SmartHomeClimateRawData";

        [Ignore]
        [TestMethod]
        public void RecalculateRowKeys()
        {
            var controller = new CosmosDBController(storageUri, tableName, storageAccountName, storageAccountKey);
            var data = controller.ReadData("RowKey lt '50000000000000'");

            foreach (var item in data)
            {
                try
                {
                    controller.WriteData(item.PartitionKey, Converters.ConvertDateTimeToReverseRowKey(item.Time), item.Value, item.Time);
                    controller.DeleteData(item.PartitionKey, item.RowKey);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(item.PartitionKey + " - " + item.RowKey);
                    throw ex;
                }
            }
        }
    }
}