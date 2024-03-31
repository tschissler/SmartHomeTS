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
mqttClient.ApplicationMessageReceivedAsync += MqttMessageReceived;

var topics = new List<string> { "your/topic/1", "your/topic/2", "your/topic/#" }; 

await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("data/charging/#").Build());
await mqttClient.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("data/electricity/envoym3").Build());

Console.WriteLine("    ...Done");
Console.WriteLine("Waiting for first data to start calculation");
Thread.Sleep(Timeout.Infinite);



async Task MqttMessageReceived(MqttApplicationMessageReceivedEventArgs args)
{
    string payload = args.ApplicationMessage.ConvertPayloadToString();
    var topic = args.ApplicationMessage.Topic;
    var time = DateTime.Now;

    Console.WriteLine($"Received message from {topic} at {time}: {payload}");

    try
    {
        currentChargingSituation.PreferedChargingStation = ChargingStation.Outside;
        currentChargingSituation.MaximumGridChargingPercent = 0;
        currentChargingSituation.BatteryMinLevel = 25;

        if (topic == "data/electricity/envoym3")
        {
            var pvData = JsonSerializer.Deserialize<EnphaseData>(payload);
            currentChargingSituation.GridPower = (int)(pvData.PowerFromGrid / 1000);
            currentChargingSituation.BatteryLevel = pvData.BatteryLevel;
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
