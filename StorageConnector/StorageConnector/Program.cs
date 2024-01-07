using StorageController;

namespace StorageConnector
{
    public class Program
    {
        private static CosmosDBController cosmosDBController;
        private static CosmosDBController cosmosDBStringController;

        static async Task Main(string[] args)
        {
            cosmosDBController = new CosmosDBController(
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri,
                "SmartHomeClimateRawData",
                "smarthomestorageprod",
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey);

            cosmosDBStringController = new CosmosDBController(
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri,
                "SmartHomeStatusRawData",
                "smarthomestorageprod",
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey);

            var mqttController = new MqttController("smarthomepi2", 32004, "Smarthome.StorageConnector");
            mqttController.OnDataUpdated += MqttController_OnDataUpdated;
            mqttController.OnStringDataUpdated += MqttController_OnStringDataUpdated;

            Thread.Sleep(Timeout.Infinite);
        }

        private static void MqttController_OnDataUpdated(string topic, double value, DateTime time)
        {
            var topicParts = topic.Split('/');
            var partitionKey = topicParts[1]+"/"+topicParts[2];
            cosmosDBController.WriteData(partitionKey, Converters.ConvertDateTimeToReverseRowKey(time), value, time);
            Console.WriteLine($"Wrote data to storage table {cosmosDBController.TableName}, time: {time.ToString("yyyy-MM-ddTHH:mm:ss")} | value: {value}");
        }
        private static void MqttController_OnStringDataUpdated(string topic, string value, DateTime time)
        {
            var topicParts = topic.Split('/');
            var partitionKey = topicParts[1] + "/" + topicParts[2];
            cosmosDBStringController.WriteData(partitionKey, Converters.ConvertDateTimeToReverseRowKey(time), value, time);
            Console.WriteLine($"Wrote data to storage table {cosmosDBStringController.TableName}, time: {time.ToString("yyyy-MM-ddTHH:mm:ss")} | value: {value}");
        }
    }
}