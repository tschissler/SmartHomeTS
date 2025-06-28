#include <Arduino.h>
#include <WiFi.h>
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>
#include <ESP32-TWAI-CAN.hpp>
#include <ArduinoJson.h>

#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"
#include "ESP32Helpers.h"
#include "soc/soc.h"
#include "soc/rtc_cntl_reg.h"

// Pin configuration - define your CAN bus pins here
#define CAN_RX_PIN 35
#define CAN_TX_PIN 5

// Hoval protocol constants based on panel settings
#define HOVAL_NODE_ID 113  // From PROT_PORT "3 113"
#define REQUEST 0x40
#define ANSWER 0x42
#define SET_REQUEST 0x46

CanFrame rxFrame;

const char *version = CANBUSGATEWAY_VERSION;
String chipID = "";

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);

WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP);
MQTTClientLib *mqttClientLib = nullptr;

static int otaInProgress = 0;
static bool otaEnable = OTA_ENABLED != "false";
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;

static String baseTopic = "data";
static String sensorName = "HovalWP_M3";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/CANBusGateway";
static String mqtt_ConfigTopic = "config/CANBusGateway/{ID}/DeviceName";

unsigned long lastDataPublishTime = 0;
const unsigned long DATA_PUBLISH_INTERVAL = 60000; // Publish data every minute

// Debug mode - set to true to log all CAN messages
bool debugMode = true;

// Heat pump data structure based on Hoval datapoints
struct DataPointDefinition {
  uint8_t  id;
  uint8_t  functionGroup;
  uint8_t  functionNumber;
  int16_t  dataPointId;
  String   dataPointName;
  uint8_t  type;
  uint8_t  decimals;
  String   unit;
  uint16_t refreshRateInSeconds;
  uint32_t value;
  time_t   lastUpdated;
};

DataPointDefinition dataPointDefs[] = {
  // { FG,   FN,   DP-ID,     "Name",       Type, Dec, "Unit" }
  {  1, 0x00, 0x00, 0x0000,    "Aussenfühler Temperatur", 1,   1,   "°C", 60   },
  {  2, 0x00, 0x00, 0x0002,    "Vorlauf-Ist Temp."      , 1,   1,   "°C", 60   },
};
struct HovalData
{
  // System temperatures
  float outsideTemp = 0; // Outside temperature (°C)
};

HovalData hovalData;

String extractVersionFromUrl(String url)
{
  int lastUnderscoreIndex = url.lastIndexOf('_');
  int lastDotIndex = url.lastIndexOf('.');

  if (lastUnderscoreIndex != -1 && lastDotIndex != -1 && lastDotIndex > lastUnderscoreIndex)
  {
    return url.substring(lastUnderscoreIndex + 1, lastDotIndex);
  }

  return ""; // Return empty string if the pattern is not found
}

void mqttCallback(String &topic, String &payload)
{
  Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

  if (topic == mqtt_ConfigTopic)
  {
    sensorName = payload;
    Serial.println("Device name set to: " + sensorName);
    return;
  }

  if (topic == mqtt_OTAtopic)
  {
    if (otaInProgress || !otaEnable)
    {
      if (otaInProgress)
        Serial.println("OTA in progress, ignoring message");
      if (!otaEnable)
        Serial.println("OTA disabled, ignoring message");
      return;
    }

    String updateVersion = extractVersionFromUrl(payload);
    Serial.println("Current firmware version is " + String(version));
    Serial.println("New firmware version is " + updateVersion);
    if (strcmp(version, updateVersion.c_str()))
    {
      // Trigger OTA Update
      const char *firmwareUrl = payload.c_str();
      Serial.println("New firmware available, starting OTA Update from " + String(firmwareUrl));
      otaInProgress = true;
      bool result = AzureOTAUpdater::UpdateFirmwareFromUrl(firmwareUrl);
      if (result)
      {
        Serial.println("OTA Update successfully initiated, waiting to be finished");
      }
    }
    else
    {
      Serial.println("Firmware is up to date");
    }
  }
  else
  {
    Serial.println("Unknown topic, ignoring message");
  }
}

void connectToMQTT()
{
  if (WiFi.status() != WL_CONNECTED)
  {
    wifiLib.connect();
  }
  mqttClientLib->connect({mqtt_ConfigTopic, mqtt_OTAtopic});
  Serial.println("MQTT Client is connected");
}

void setupCanBus()
{
  // Initialize CAN Bus communication
  Serial.println("Setting up CAN Bus...");

  // Configure CAN bus
  ESP32Can.setPins(CAN_TX_PIN, CAN_RX_PIN);
  
  // begin with custom timing instead of a TwaiSpeed enum
  if (ESP32Can.begin(TWAI_SPEED_50KBPS))
  {
    Serial.println("CAN Bus initialized at 50 kbps");
  }
  else
  {
    Serial.println("Failed to initialize CAN Bus at 50 kbps");
    return;
  }

  // If you're not receiving messages, you might need to try different speeds
  // Common speeds for HVAC systems: 125kbps, 250kbps, 500kbps
}

void sendHovalPollFrame()
{
  Serial.println("Sending Hoval poll frame...");
  mqttClientLib->publish((baseTopic + "/hoval/" + sensorName + "/status/polling").c_str(), "Sending poll frame", true, 0);
  // Poll outside temperature sensor (function_group=0,function_number=0,datapoint=0)
  const uint8_t function_group = 0;
  const uint8_t function_number = 0;
  const uint16_t datapoint = 0;

  // Build payload: [counter=0x01, REQUEST, group, number, datapoint high, datapoint low]
  uint8_t payload[6];
  payload[0] = 0x01;
  payload[1] = REQUEST;
  payload[2] = function_group;
  payload[3] = function_number;
  payload[4] = (uint8_t)(datapoint >> 8);
  payload[5] = (uint8_t)(datapoint & 0xFF);

  // Build extended identifier: (0x1F0 << 16) | 0x0801 (fixed address)
  CanFrame pollFrame;
  pollFrame.identifier = (0x1FE << 16) | 0x0801;
  pollFrame.extd = true;
  pollFrame.rtr = false;
  pollFrame.data_length_code = 6;
  memcpy(pollFrame.data, payload, 6);

  if (ESP32Can.writeFrame(pollFrame)) {
    Serial.println("Poll-Frame sent for outside temperature");
  } else {
    Serial.println("Error sending Poll-Frame for outside temperature");
    mqttClientLib->publish((baseTopic + "/hoval/" + sensorName + "/status/error").c_str(), "Error sending Poll-Frame", true, 0);
  }
}

// Function to decode a Hoval value from CAN data
float decodeHovalValue(const uint8_t *data, int startByte, float factor = 1.0)
{
  // Hoval values are typically stored as 16-bit integers (MSB first)
  int16_t rawValue = (data[startByte] << 8) | data[startByte + 1];
  return (float)rawValue * factor;
}

// Function to decode Hoval heat pump data from CAN frames
void decodeHovalData(const CanFrame &frame)
{
  if (frame.data[0] == 01 && frame.data[1] == ANSWER)
  {
    Serial.println("Hoval heat pump data received");
    // Extract the function group and number
    uint8_t functionGroup = frame.data[2];
    uint8_t functionNumber = frame.data[3];
    uint16_t dataPointId = (frame.data[4] << 8) | frame.data[5];
    int16_t rawValue = (frame.data[6] << 8) | frame.data[7];

    Serial.print("Function Group: ");
    Serial.print(functionGroup, HEX);
    Serial.print(", Function Number: ");
    Serial.print(functionNumber, HEX);
    Serial.print(", Data Point ID: ");
    Serial.print(dataPointId, HEX);
    Serial.print(", Raw Value: ");
    Serial.println(rawValue);

    auto it = std::find_if(std::begin(dataPointDefs),
                           std::end(dataPointDefs),
                           [&](DataPointDefinition &dp) {
                             return dp.functionGroup == functionGroup &&
                                    dp.functionNumber == functionNumber &&
                                    dp.dataPointId   == dataPointId;
                           });
    if (it != std::end(dataPointDefs)) {
      it->value       = rawValue;
      it->lastUpdated = time(nullptr);
    }
  }
}

// Function to publish heat pump data to MQTT
void publishHovalData()
{
  // Create a JSON document for the data
  JsonDocument jsonDoc;
  jsonDoc["outside_temp"] = hovalData.outsideTemp;

  // Serialize JSON to string
  String jsonString;
  serializeJson(jsonDoc, jsonString);

  // Publish to MQTT
  String topic = baseTopic + "/hoval/" + sensorName + "/data";
  mqttClientLib->publish(topic.c_str(), jsonString, true, 0); // Retained message with QoS 0

  Serial.println("Published Hoval data to MQTT");
  Serial.println(jsonString);
}

// Update the processCanMessages function
void processCanMessages()
{
  // Check if CAN messages are available
  if (ESP32Can.readFrame(rxFrame, 1000))
  {

    // Log raw CAN frame for debugging
    if (debugMode)
    {
      Serial.print("CAN frame: 0x");
      Serial.print(rxFrame.identifier, HEX);
      Serial.print(" Data: ");

      for (int i = 0; i < rxFrame.data_length_code; i++)
      {
        if (rxFrame.data[i] < 16)
          Serial.print("0");
        Serial.print(rxFrame.data[i], HEX);
        Serial.print(" ");
      }
      Serial.println();

      String rawTopic = baseTopic + "/hoval/" + sensorName + "/raw/" + String(rxFrame.identifier, HEX);

      // Create a more descriptive payload with ID name
      JsonDocument jsonDoc;
      jsonDoc["id"] = "0x" + String(rxFrame.identifier, HEX);

      // Add raw data bytes
      JsonArray dataArray = jsonDoc.createNestedArray("data");
      for (int i = 0; i < rxFrame.data_length_code; i++)
      {
        dataArray.add(rxFrame.data[i]);
      }

      // Add decoded value if possible
      if (rxFrame.data_length_code >= 2)
      {
        float value = decodeHovalValue(rxFrame.data, 0, 0.1);
        jsonDoc["value"] = value;
      }
      jsonDoc["value"] = "0x" + String(rxFrame.data, HEX); // Example, replace with actual decoding logic
      String jsonString;
      serializeJson(jsonDoc, jsonString);
      mqttClientLib->publish(rawTopic.c_str(), jsonString, false, 0, false);
    }


    // Decode the Hoval heat pump data
    decodeHovalData(rxFrame);

    // // Periodically publish aggregated heat pump data
    // unsigned long currentMillis = millis();
    // if (currentMillis - lastDataPublishTime >= DATA_PUBLISH_INTERVAL)
    // {
    //   publishHovalData();
    //   lastDataPublishTime = currentMillis;
    // }
  }

  else
  {
    // No CAN messages received, you can handle this case if needed
    if (debugMode)
    {
      Serial.println("No CAN messages received");
    }
  }
}

void setup()
{
  WRITE_PERI_REG(RTC_CNTL_BROWN_OUT_REG, 0); // Disable brownout detector
  Serial.begin(115200);
  Serial.print("CanBusGateway version ");
  Serial.println(version);

  chipID = ESP32Helpers::getChipId();
  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);

  mqtt_ConfigTopic.replace("{ID}", chipID);

  // Connect to WiFi
  Serial.println("Connecting to WiFi...");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();
  String ssid = wifiLib.getSSID();
  Serial.println("Connected to WiFi SSID: " + ssid);

  // Set up MQTT
  String mqttClientID = "ESP32CanBusGatewayClient_" + chipID;
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT();

  // Initialize NTPClient
  timeClient.begin();
  timeClient.setTimeOffset(0); // Set your time offset from UTC in seconds
  timeClient.update();

  // Setup CAN Bus
  setupCanBus();

  // Publish device info to MQTT
  mqttClientLib->publish(("meta/" + sensorName + "/version/CanBusGateway").c_str(), String(version), true, 2);

  Serial.println("Setup complete");
}

void loop()
{
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();

  if (otaInProgress < 0)
  {
    Serial.println("OTA update failed");
  }

  if (otaInProgress != 1)
  {
    timeClient.update();

        // Nur alle 5 Sekunden einen Poll senden
    // static unsigned long lastPoll = 0;
    // if (millis() - lastPoll > 20000)
    // {
    //   sendHovalPollFrame();
    //   lastPoll = millis();
    // }

    // Process CAN messages
    processCanMessages();

    // Check MQTT connection
    if (!mqttClientLib->loop())
    {
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT();
    }
  }

  delay(100); // Small delay to prevent CPU overload
}