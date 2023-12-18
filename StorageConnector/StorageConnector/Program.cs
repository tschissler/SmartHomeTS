namespace StorageConnector
{
    public class Program
    {
        private static CosmosDBController cosmosDBController;

        static async Task Main(string[] args)
        {
            cosmosDBController = new CosmosDBController(
                   "https://smarthometsstorage.table.cosmos.azure.com:443/",
                   "SmartHomeData",
                   "smarthometsstorage",
                   "yRZ84NCODris5jSJpP1tbZO1zxVkTTRSEsn4Yiu5TNyKFIToLOaMDe6whunduEzFT3tFwm95X4lcACDbRQDdPQ==");

            var mqttController = new MqttController("smarthomepi2", 32004, "Smarthome.StorageConnector");
            mqttController.OnDataUpdated += MqttController_OnDataUpdated;

            Thread.Sleep(Timeout.Infinite);
        }

        private static void MqttController_OnDataUpdated(string topic, decimal value, DateTime time)
        {
            var topicParts = topic.Split('/');
            var partitionKey = topicParts[1]+"/"+topicParts[2];
            cosmosDBController.WriteData(partitionKey, time.ToString("yyyyMMddHHmmss"), value, time);
        }
    }
}