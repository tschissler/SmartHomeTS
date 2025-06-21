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

// Hoval protocol constants based on panel settings
#define HOVAL_NODE_ID 113  // From PROT_PORT "3 113"
#define REQUEST 0x40
#define ANSWER 0x42
#define SET_REQUEST 0x46

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
  float outsideTemp = 0;      // Outside temperature (°C)
  float flowTemp = 0;         // Flow temperature (°C)
  float returnTemp = 0;       // Return temperature (°C)
  float hotWaterTemp = 0;     // Hot water temperature (°C)
  
  // System status
  uint8_t heatPumpStatus = 0; // Heat pump status
  uint8_t modulation = 0;     // Modulation percentage
  
  // Last update time for each value
  unsigned long outsideTempUpdated = 0;
  unsigned long flowTempUpdated = 0;
  unsigned long returnTempUpdated = 0;
  unsigned long hotWaterTempUpdated = 0;
  unsigned long statusUpdated = 0;
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
  
  // Based on the Hoval panel settings:
  // BAUDRATE "2" = 250 kbps
  // PROT_PORT "3 113" (Protocol 3 = CAN, Node ID 113)
  ESP32Can.setSpeed(TWAI_SPEED_250KBPS);
  Serial.printf("Setting CAN bus to 250 kbps based on Hoval panel settings (BAUDRATE=2)\n");
    
  if(ESP32Can.begin()) {
    Serial.printf("CAN Bus initialized at 250 kbps\n");
  } else {
    Serial.printf("Failed to initialize CAN Bus at 250 kbps\n");
    
    // Fallback to trying other speeds
    const TwaiSpeed fallbackSpeeds[] = {TWAI_SPEED_125KBPS, TWAI_SPEED_500KBPS};
    const char* speedNames[] = {"125 kbps", "500 kbps"};
    
    bool connected = false;
    for (int i = 0; i < 2 && !connected; i++) {
      ESP32Can.setSpeed(fallbackSpeeds[i]);
      Serial.printf("Trying fallback CAN bus speed at %s...\n", speedNames[i]);
      
      if(ESP32Can.begin()) {
        Serial.printf("CAN Bus initialized at %s\n", speedNames[i]);
        connected = true;
      } else {
        Serial.printf("Failed to initialize CAN Bus at %s\n", speedNames[i]);
        delay(500);
      }
    }
    
    if (!connected) {
      Serial.println("Failed to initialize CAN Bus at any speed!");
      return;
    }
  }
  
  Serial.printf("Hoval node ID appears to be 113 based on panel settings (PROT_PORT=3 113)\n");
}

// Function to decode a Hoval value from CAN data
float decodeHovalValue(const uint8_t *data, int startByte, float factor = 1.0) {
  // Hoval values are typically stored as 16-bit integers (MSB first)
  int16_t rawValue = (data[startByte] << 8) | data[startByte + 1];
  return (float)rawValue * factor;
}

// Function to decode Hoval heat pump data from CAN frames
void decodeHovalData(const CanFrame &frame) {
  // First, check if this is a response from our Hoval device (Node ID 113)
  uint8_t msgId = (frame.identifier >> 16) & 0xFF;
  uint8_t deviceType = (frame.identifier >> 8) & 0xFF;
  uint8_t deviceId = frame.identifier & 0xFF;
  
  if (debugMode) {
    Serial.printf("Parsed CAN ID: msgId=0x%02X, deviceType=0x%02X, deviceId=0x%02X\n", 
                  msgId, deviceType, deviceId);
  }
  
  // Check if this is a message in the Hoval protocol format (msgId=0x1F)
  if (msgId == 0x1F) {
    // Check if we have enough data to process
    if (frame.data_length_code < 2) return;
    
    // Check if this is a single frame message (frame.data[0] >> 3 == 0)
    uint8_t msg_len = frame.data[0] >> 3;
    if (msg_len == 0 && frame.data_length_code > 2) {
      // Check if this is a response (ANSWER = 0x42)
      if (frame.data[1] == ANSWER) {
        // Extract function group, function number, and datapoint
        if (frame.data_length_code >= 7) { // Need at least 7 bytes for header + data
          uint8_t functionGroup = frame.data[2];
          uint8_t functionNumber = frame.data[3];
          uint16_t datapoint = (frame.data[4] << 8) | frame.data[5];
          
          if (debugMode) {
            Serial.printf("Received response for datapoint (%d,%d,%d)\n", 
                         functionGroup, functionNumber, datapoint);
          }
          
          unsigned long currentTime = millis();
          
          // Process based on the function group, function number, and datapoint
          if (functionGroup == 0 && functionNumber == 0 && datapoint == 0) {
            // Outside temperature sensor (AF 1)
            hovalData.outsideTemp = decodeHovalValue(&frame.data[6], 0, 0.1);
            hovalData.outsideTempUpdated = currentTime;
            if (debugMode) {
              Serial.printf("Decoded outside temp: %.1f°C\n", hovalData.outsideTemp);
            }
          }
          else if (functionGroup == 1 && functionNumber == 0 && datapoint == 2) {
            // Flow temperature
            hovalData.flowTemp = decodeHovalValue(&frame.data[6], 0, 0.1);
            hovalData.flowTempUpdated = currentTime;
            if (debugMode) {
              Serial.printf("Decoded flow temp: %.1f°C\n", hovalData.flowTemp);
            }
          }
          else if (functionGroup == 10 && functionNumber == 1 && datapoint == 8) {
            // Return temperature
            hovalData.returnTemp = decodeHovalValue(&frame.data[6], 0, 0.1);
            hovalData.returnTempUpdated = currentTime;
            if (debugMode) {
              Serial.printf("Decoded return temp: %.1f°C\n", hovalData.returnTemp);
            }
          }
          else if (functionGroup == 2 && functionNumber == 0 && datapoint == 4) {
            // Hot water temperature
            hovalData.hotWaterTemp = decodeHovalValue(&frame.data[6], 0, 0.1);
            hovalData.hotWaterTempUpdated = currentTime;
            if (debugMode) {
              Serial.printf("Decoded hot water temp: %.1f°C\n", hovalData.hotWaterTemp);
            }
          }
          else if (functionGroup == 10 && functionNumber == 1 && datapoint == 2053) {
            // Heat pump status
            hovalData.heatPumpStatus = frame.data[6]; // First byte of value
            hovalData.statusUpdated = currentTime;
            if (debugMode) {
              Serial.printf("Decoded heat pump status: %d\n", hovalData.heatPumpStatus);
            }
          }
          else if (functionGroup == 10 && functionNumber == 1 && datapoint == 20052) {
            // Modulation
            hovalData.modulation = frame.data[6]; // First byte of value
            hovalData.statusUpdated = currentTime;
            if (debugMode) {
              Serial.printf("Decoded modulation: %d%%\n", hovalData.modulation);
            }
          }
        }
      }
    }
  } 
  // Also handle the standard IDs for backward compatibility
  else {
    unsigned long currentTime = millis();
    
    switch(frame.identifier) {
      // Group 1: Basic system temperatures (typically 0x180 + node ID)
      case 0x181: // Outside temperature
        if (frame.data_length_code >= 2) {
          hovalData.outsideTemp = decodeHovalValue(frame.data, 0, 0.1);
          hovalData.outsideTempUpdated = currentTime;
          if (debugMode) {
            Serial.printf("Decoded outside temp (legacy): %.1f°C\n", hovalData.outsideTemp);
          }
        }
        break;
    }
  }
}

// Function to publish heat pump data to MQTT
void publishHovalData() {
  // Create a JSON document for the data
  JsonDocument jsonDoc;
  unsigned long currentTime = millis();
  
  // Only include data points that have been updated in the last 5 minutes
  const unsigned long MAX_DATA_AGE = 300000; // 5 minutes
  
  if (currentTime - hovalData.outsideTempUpdated < MAX_DATA_AGE) {
    jsonDoc["outside_temp"] = hovalData.outsideTemp;
  }
  
  if (currentTime - hovalData.flowTempUpdated < MAX_DATA_AGE) {
    jsonDoc["flow_temp"] = hovalData.flowTemp;
  }
  
  if (currentTime - hovalData.returnTempUpdated < MAX_DATA_AGE) {
    jsonDoc["return_temp"] = hovalData.returnTemp;
  }
  
  if (currentTime - hovalData.hotWaterTempUpdated < MAX_DATA_AGE) {
    jsonDoc["hot_water_temp"] = hovalData.hotWaterTemp;
  }
  
  if (currentTime - hovalData.statusUpdated < MAX_DATA_AGE) {
    jsonDoc["heat_pump_status"] = hovalData.heatPumpStatus;
    jsonDoc["modulation"] = hovalData.modulation;
  }
  
  // Add a timestamp
  jsonDoc["timestamp"] = timeClient.getEpochTime();
  
  // Serialize JSON to string
  String jsonString;
  serializeJson(jsonDoc, jsonString);
  
  // Publish to MQTT
  String topic = baseTopic + "/hoval/" + sensorName + "/data";
  mqttClientLib->publish(topic.c_str(), jsonString, true, 0); // Retained message with QoS 0
    
  Serial.println("Published Hoval data to MQTT");
  Serial.println(jsonString);
}


// Function to query data from the Hoval system with node ID 113
void queryHovalData(uint8_t functionGroup, uint8_t functionNumber, uint16_t datapoint) {
  CanFrame txFrame;
  
  // Prepare CAN frame
  txFrame.identifier = (0x1F0 << 16) | (0x0100 * HOVAL_NODE_ID); // Use node ID from panel settings
  txFrame.extd = 1; // Extended ID
  txFrame.rtr = 0; // Not a remote frame
  txFrame.data_length_code = 6;
  
  // Set data bytes according to Hoval protocol
  txFrame.data[0] = 0x01;
  txFrame.data[1] = REQUEST; // 0x40
  txFrame.data[2] = functionGroup;
  txFrame.data[3] = functionNumber;
  txFrame.data[4] = (datapoint >> 8) & 0xFF; // MSB of datapoint
  txFrame.data[5] = datapoint & 0xFF; // LSB of datapoint
  
  // Send the query
  if (ESP32Can.writeFrame(txFrame)) {
    if (debugMode) {
      Serial.printf("Sent query to Hoval node %d for datapoint (%d,%d,%d)\n", HOVAL_NODE_ID, functionGroup, functionNumber, datapoint);
    }
  } else {
    Serial.println("Failed to send query to Hoval system");
  }
}

// Update the processCanMessages function
void processCanMessages() {
  static unsigned long lastQueryTime = 0;
  const unsigned long QUERY_INTERVAL = 10000; // Query every 10 seconds
  
  // Periodically query data from Hoval system
  unsigned long currentMillis = millis();
  if (currentMillis - lastQueryTime >= QUERY_INTERVAL) {
    // Query outside temperature
    queryHovalData(0, 0, 0);
    delay(100);
    
    // Query flow temperature
    queryHovalData(1, 0, 2);
    delay(100);
    
    // Query hot water temperature
    queryHovalData(2, 0, 4);
    delay(100);
    
    // Query heat pump status
    queryHovalData(10, 1, 2053);
    
    lastQueryTime = currentMillis;
  }
  
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
      for (int i = 0; i < rxFrame.data_length_code; i++) {
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