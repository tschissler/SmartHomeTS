using StorageController;

namespace StorageConnector
{
    public class Program
    {
        private static TableStorageController dBController;
        private static TableStorageController dBStringController;

        static async Task Main(string[] args)
        {
            Console.Write("Starting Storage Controller ...");
            dBController = new TableStorageController(
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri,
                "SmartHomeClimateRawData",
                "smarthomestorageprod",
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey);

            dBStringController = new TableStorageController(
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageUri,
                "SmartHomeStatusRawData",
                "smarthomestorageprod",
                SmartHomeHelpers.Configuration.Storage.SmartHomeStorageKey);

            var mqttController = new MqttController("smarthomepi2", 32004, "Smarthome.StorageConnector");
            mqttController.OnDataUpdated += MqttController_OnDataUpdated;
            mqttController.OnStringDataUpdated += MqttController_OnStringDataUpdated;

            await Console.Out.WriteLineAsync("Running");
            Thread.Sleep(Timeout.Infinite);
        }

        private static void MqttController_OnDataUpdated(string topic, double value, DateTime time)
        {
            var topicParts = topic.Split('/');
            var partitionKey = topicParts[1]+"/"+topicParts[2];
            dBController.WriteData(partitionKey, Converters.ConvertDateTimeToReverseRowKey(time), value, time);
            Console.WriteLine($"Wrote data to storage table {dBController.TableName}, time: {time.ToString("yyyy-MM-ddTHH:mm:ss")} | value: {value}");
        }
        private static void MqttController_OnStringDataUpdated(string topic, string value, DateTime time)
        {
            var topicParts = topic.Split('/');
            var partitionKey = topicParts[1] + "/" + topicParts[2];
            dBStringController.WriteData(partitionKey, Converters.ConvertDateTimeToReverseRowKey(time), value, time);
            Console.WriteLine($"Wrote data to storage table {dBStringController.TableName}, time: {time.ToString("yyyy-MM-ddTHH:mm:ss")} | value: {value}");
        }
    }
}