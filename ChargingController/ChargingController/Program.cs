using ChargingController;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using SharedContracts;
using System.Text.Json;


IMqttClient mqttClient;
ChargingInput currentChargingSituation = new ChargingInput();
int previousOutsideChargingCurrent = -1;
int previousInsideChargingCurrent = -1;
DateTime lastOutsideSetTime = DateTime.MinValue;
DateTime lastInsideSetTime = DateTime.MinValue;
const int minimumSetIntervalSeconds = 10;

Console.WriteLine("ChargingController started");

// Listen to Keba and Envoy MQTT messages and new configuration setting
Console.WriteLine("  - Connecting to MQTT Broker");

var factory = new MqttFactory();
mqttClient = factory.CreateMqttClient();
await MQTTConnectAsync();

Console.WriteLine("    ...Done");
Console.WriteLine("Waiting for first data to start calculation");

while (true)
{
    if (!mqttClient.IsConnected)
    {
        Console.WriteLine("MQTT connection lost. Reconnecting...");
        await MQTTConnectAsync();
    }
    await Task.Delay(1000);
}


async Task MqttMessageReceived(MqttApplicationMessageReceivedEventArgs args)
{
    string payload = args.ApplicationMessage.ConvertPayloadToString();
    var topic = args.ApplicationMessage.Topic;
    var time = DateTime.Now;

    Console.WriteLine($"Received message from {topic} at {time}: {payload}");

    try
    {
        //currentChargingSituation.PreferedChargingStation = ChargingStation.Outside;
        //currentChargingSituation.MaximumGridChargingPercent = 0;
        //currentChargingSituation.BatteryMinLevel = 25;
        //currentChargingSituation.PreferedChargingBatteryLevel = 25;

        int value;
        if (topic == "config/charging/MaximumGridChargingPercent" && int.TryParse(payload, out value))
        {
            currentChargingSituation.MaximumGridChargingPercent = value;
            Console.WriteLine($"########## Set MaximumGridChargingPercent to {value}");
        }
        else if (topic == "config/charging/BatteryMinLevel" && int.TryParse(payload, out value))
        {
            currentChargingSituation.BatteryMinLevel = value;
            Console.WriteLine($"########## Set BatteryMinLevel to {value}");
        }
        else if (topic == "config/charging/PrefereChargingBatteryLevel" && int.TryParse(payload, out value))
        {
            currentChargingSituation.PreferedChargingBatteryLevel = value;
            Console.WriteLine($"########## Set PrefereChargingBatteryLevel to {value}");
        }
        else if (topic == "config/charging/PreferedChargingStation")
        {
            currentChargingSituation.PreferedChargingStation = payload == "Inside" ? ChargingStation.Inside : ChargingStation.Outside;
            Console.WriteLine($"########## Set PreferedChargingStation to {payload}");
        }
        else if (topic == "config/charging/ManualCurrentManualCurrent" && int.TryParse(payload, out value))
        {
            currentChargingSituation.ManualCurrent = value;
            Console.WriteLine($"########## Set ManualCurrent to {value}");
        }

        else if (topic == "data/electricity/envoym3")
        {
            var pvData = JsonSerializer.Deserialize<EnphaseData>(payload);
            currentChargingSituation.GridPower = (int)(pvData.PowerFromGrid / 1000);
            currentChargingSituation.BatteryLevel = pvData.BatteryLevel;
            currentChargingSituation.PowerFromBattery = (int)pvData.PowerFromBattery;
        }

        else if (topic == "data/charging/KebaGarage")
        {
            var kebaGarageData = JsonSerializer.Deserialize<ChargingGetData>(payload);
            currentChargingSituation.InsideCurrentChargingPower = kebaGarageData.CurrentChargingPower;
            currentChargingSituation.InsideConnected = kebaGarageData.CarIsPlugedIn;
        }

        else if (topic == "data/charging/KebaOutside")
        {
            var kebaOutsideData = JsonSerializer.Deserialize<ChargingGetData>(payload);
            currentChargingSituation.OutsideCurrentChargingPower = kebaOutsideData.CurrentChargingPower;
            currentChargingSituation.OutsideConnected = kebaOutsideData.CarIsPlugedIn;
        }

        var chargingResult = await ChargingDecisionsMaker.CalculateChargingData(currentChargingSituation);
        if (chargingResult.InsideChargingCurrentmA != previousInsideChargingCurrent)
        {
            if (DateTime.Now.Subtract(lastInsideSetTime).Seconds > minimumSetIntervalSeconds)
            {
                var payloadOut = JsonSerializer.Serialize(new ChargingSetData() { ChargingCurrent = chargingResult.InsideChargingCurrentmA });
                await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic("commands/charging/KebaGarage")
                    .WithPayload(payloadOut)
                    .Build());
                Console.WriteLine($"Sent MQTT message with payload; {payloadOut}");
                previousInsideChargingCurrent = chargingResult.InsideChargingCurrentmA;
            }
            else 
            {
                Console.WriteLine($"Inside charging current was set too recently. Skipping.");
            }
        }
        if (chargingResult.OutsideChargingCurrentmA != previousOutsideChargingCurrent)
        {
            if (DateTime.Now.Subtract(lastOutsideSetTime).Seconds > minimumSetIntervalSeconds)
            {
                var payloadOut = JsonSerializer.Serialize(new ChargingSetData() { ChargingCurrent = chargingResult.OutsideChargingCurrentmA });
                await mqttClient.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic("commands/charging/KebaOutside")
                    .WithPayload(payloadOut)
                    .Build());
                Console.WriteLine($"Sent MQTT message with payload; {payloadOut}");
                previousOutsideChargingCurrent = chargingResult.OutsideChargingCurrentmA;
            }
            else
            {
                Console.WriteLine($"Outside charging current was set too recently. Skipping.");
            }
        }
    } 
    catch (Exception ex) 
    {
        Console.WriteLine($"Error processing MQTT message: {ex.Message}");
    }
}

async Task MQTTConnectAsync()
{
    var mqttOptions = new MqttClientOptionsBuilder()
        .WithTcpServer("smarthomepi2", 32004)
        .WithClientId("Smarthome.ChargingController")
        .Build();

    while (true)
    {
        if (mqttClient.IsConnected)
            break;
        try
        {
            await mqttClient.ConnectAsync(mqttOptions);
            if (mqttClient.IsConnected)
            {
                Console.WriteLine("Connected to MQTT Broker.");

                mqttClient.ApplicationMessageReceivedAsync += MqttMessageReceived;

                await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("data/charging/#").Build());
                await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("data/electricity/envoym3").Build());
                await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("config/charging/#").Build());
                break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error connecting to MQTT Broker: {ex.Message}");
            Thread.Sleep(5000);
        }
    }
}
