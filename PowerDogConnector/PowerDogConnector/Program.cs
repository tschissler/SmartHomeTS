using MqttLib;
using PowerDogLib;
using System;

namespace PowerDogConnector
{
    public class Program
    {
        private static PowerDog powerDog;
        private static MqttController mqttController;

        static async Task Main(string[] args)
        {
            Dictionary<string, string> sensorKeys = new()
            {
                { "Bezug", "iec1107_1457430339" }, // Vom Zähler
                { "Erzeugung", "pv_global_1454412642" },  // Vom Wechselrichter
                { "Eigenverbrauch", "arithmetic_1457431399" },
                { "Verbrauchgesamt", "arithmetic_1457432629" },
                { "lieferung", "iec1107_1457430562" } // Vom Zähler
            };

            UriBuilder uri = new("http", "powerdog", 20000);

            mqttController = new MqttController("smarthomepi2", 32004, "Smarthome.PowerDogConnector");
            powerDog = new(sensorKeys, uri.Uri, "admin");

            var timer = new Timer(SendDataMessage, null, 0, 1000);

            Thread.Sleep(Timeout.Infinite);
        }

        private static void SendDataMessage(object? state)
        {
            var data = powerDog.ReadSensorsData();
            if (data != null) 
            {
                mqttController.SendMessage("data/PVM3/PVProduction", data.PVProduction);
                mqttController.SendMessage("data/PVM3/GridConsumption", data.GridDemand-data.GridSupply);
            }
        }
    }
}