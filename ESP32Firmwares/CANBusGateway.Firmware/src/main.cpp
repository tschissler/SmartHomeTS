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
#define CAN_RX_PIN 22
#define CAN_TX_PIN 23

CanFrame rxFrame;

const char* version = CANBUSGATEWAY_VERSION;
String chipID = "";
 
// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);

WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP);
MQTTClientLib* mqttClientLib = nullptr;

static int otaInProgress = 0;
static bool otaEnable = true;
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
struct HovalData {
  // System temperatures
  float outsideTemp = 0;            // Outside temperature (°C)
};

HovalData hovalData;

String extractVersionFromUrl(String url) {
    int lastUnderscoreIndex = url.lastIndexOf('_');
    int lastDotIndex = url.lastIndexOf('.');

    if (lastUnderscoreIndex != -1 && lastDotIndex != -1 && lastDotIndex > lastUnderscoreIndex) {
        return url.substring(lastUnderscoreIndex + 1, lastDotIndex);
    }

    return ""; // Return empty string if the pattern is not found
}

void mqttCallback(String &topic, String &payload) {
    Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

    if (topic == mqtt_ConfigTopic) {
      sensorName = payload;
      Serial.println("Device name set to: " + sensorName);
      return;
    } 

    if (topic == mqtt_OTAtopic) {
      if (otaInProgress || !otaEnable) {
        if (otaInProgress)
          Serial.println("OTA in progress, ignoring message");
        if (!otaEnable)
          Serial.println("OTA disabled, ignoring message");
        return;
      }
  
      String updateVersion = extractVersionFromUrl(payload);
      Serial.println("Current firmware version is " + String(version));
      Serial.println("New firmware version is " + updateVersion);
      if(strcmp(version, updateVersion.c_str())) {
          // Trigger OTA Update
          const char *firmwareUrl = payload.c_str();
          Serial.println("New firmware available, starting OTA Update from " + String(firmwareUrl));
          otaInProgress = true;
          bool result = AzureOTAUpdater::UpdateFirmwareFromUrl(firmwareUrl);
          if (result) {
            Serial.println("OTA Update successfully initiated, waiting to be finished");
          }
      }
      else {
        Serial.println("Firmware is up to date");
      }
    }
    else {
      Serial.println("Unknown topic, ignoring message");
    }
}

void connectToMQTT() {
  if (WiFi.status() != WL_CONNECTED) {
    wifiLib.connect();
  }
  mqttClientLib->connect({mqtt_ConfigTopic, mqtt_OTAtopic});
  Serial.println("MQTT Client is connected");
}

void setupCanBus() {
  // Initialize CAN Bus communication
  Serial.println("Setting up CAN Bus...");
  
  // Configure CAN bus
  ESP32Can.setPins(CAN_TX_PIN, CAN_RX_PIN);
  ESP32Can.setSpeed(TWAI_SPEED_125KBPS); // Set to 125 kbps, common for HVAC systems
  if(ESP32Can.begin()) {
  Serial.println("CAN Bus initialized at 125 kbps");
  } else {
    Serial.println("Failed to initialize CAN Bus");
    return;
  }
  
  // If you're not receiving messages, you might need to try different speeds
  // Common speeds for HVAC systems: 125kbps, 250kbps, 500kbps
}

// Function to decode a Hoval value from CAN data
float decodeHovalValue(const uint8_t *data, int startByte, float factor = 1.0) {
  // Hoval values are typically stored as 16-bit integers (MSB first)
  int16_t rawValue = (data[startByte] << 8) | data[startByte + 1];
  return (float)rawValue * factor;
}

// Function to decode Hoval heat pump data from CAN frames
void decodeHovalData(const CanFrame &frame) {
  // Based on Hoval TopTronic E datapoints spreadsheet
  
  switch(frame.identifier) {
    // Group 1: Basic system temperatures (typically 0x180 + node ID)
    case 0x181: // Outside temperature
      if (frame.data_length_code >= 2) {
        hovalData.outsideTemp = decodeHovalValue(frame.data, 0, 0.1);
      }
      break;
  }
}

// Function to publish heat pump data to MQTT
void publishHovalData() {
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
}


// Update the processCanMessages function
void processCanMessages() {
  
  // Check if CAN messages are available
  if (ESP32Can.readFrame(rxFrame, 1000)) {
    // Decode the Hoval heat pump data
    decodeHovalData(rxFrame);

    // Log raw CAN frame for debugging
    if (debugMode) {
      Serial.print("CAN frame: 0x");
      Serial.print(rxFrame.identifier, HEX);
      Serial.print(" Data: ");
      
      for (int i = 0; i < rxFrame.data_length_code; i++) {
        if (rxFrame.data[i] < 16) Serial.print("0");
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
      for (int i = 0; i < rxFrame.identifier; i++) {
        dataArray.add(rxFrame.data[i]);
      }
      
      // Add decoded value if possible
      if (rxFrame.data_length_code >= 2) {
        float value = decodeHovalValue(rxFrame.data, 0, 0.1);
        jsonDoc["value"] = value;
      }
      
      String jsonString;
      serializeJson(jsonDoc, jsonString);
      mqttClientLib->publish(rawTopic.c_str(), jsonString, false, 0);      
    }
  }
  
  // Periodically publish aggregated heat pump data
  unsigned long currentMillis = millis();
  if (currentMillis - lastDataPublishTime >= DATA_PUBLISH_INTERVAL) {
    publishHovalData();
    lastDataPublishTime = currentMillis;
  }
}

void setup() {
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

void loop() {
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();

  if (otaInProgress < 0) {
    Serial.println("OTA update failed");
  }

  if (otaInProgress != 1) {
    timeClient.update();

    // Process CAN messages
    processCanMessages();

    // Check MQTT connection
    if (!mqttClientLib->loop()) {
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT();
    }
  }
  
  delay(10); // Small delay to prevent CPU overload
}