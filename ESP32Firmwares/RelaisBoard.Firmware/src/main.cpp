#include <Arduino.h>
#include <ArduinoJson.h>
#include <map>
#include <NTPClient.h>
#include <time.h>

#include <ESP32Ping.h>
#include <esp_mac.h> 
#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"

#define NUMBER_OF_RELAYS 8
#define RELAIS_1 19  
#define RELAIS_2 18
#define RELAIS_3 5
#define RELAIS_4 17
#define RELAIS_5 16
#define RELAIS_6 4
#define RELAIS_7 0
#define RELAIS_8 15

#define waitTime 100

const char* version = FIRMWARE_VERSION;
String chipID = "";

// Room to pin mapping (will be populated from MQTT config)
std::map<String, int> roomToPinMapping;

// Array of available relay pins for easier management
const int relayPins[NUMBER_OF_RELAYS] = {
  RELAIS_1, RELAIS_2, RELAIS_3, RELAIS_4, 
  RELAIS_5, RELAIS_6, RELAIS_7, RELAIS_8
};

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);

WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP, "pool.ntp.org", 3600, 60000); // UTC+1, update every 60 seconds
std::unique_ptr<MQTTClientLib> mqttClient = nullptr;

static bool otaInProgress = false;
static bool otaEnable = true;
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;

static String baseTopic = "data/heating";
static String sensorName = "";
static String location = "";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/Relaismodule";
static String mqtt_SensornameTopic = "config/Relaismodule/Sensorname/";
static String mqtt_config_Base = "config/Relaismodule/";
static String mqtt_config_Topic = "";
static String mqtt_CommandsTopic = "commands/Heating/#";
static String mqtt_Data_Topic = "daten/Heizung/{location}/";
static bool subscribedToCommands = false;
static bool subscribedToConfig = false;

int getRelayPinByNumber(int pinNumber) {
  if (pinNumber >= 1 && pinNumber <= NUMBER_OF_RELAYS) {
    return relayPins[pinNumber - 1]; 
  }
  return -1; // Invalid pin number
}

bool updateRoomMapping(const String& jsonConfig) {
  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, jsonConfig);
  
  if (error) {
    Serial.println("Failed to parse room mapping JSON: " + String(error.c_str()));
    return false;
  }
  
  roomToPinMapping.clear();
  
  if (!doc.is<JsonArray>()) {
    Serial.println("Room mapping JSON must be an array");
    return false;
  }
  
  JsonArray rooms = doc.as<JsonArray>();
  
  for (JsonObject room : rooms) {
    if (!room["Room"].is<String>() || !room["Pin"].is<int>()) {
      Serial.println("Room mapping entry missing 'Room' or 'Pin' field");
      continue;
    }
    
    String roomName = room["Room"].as<String>();
    int pinNumber = room["Pin"].as<int>();
    
    int actualPin = getRelayPinByNumber(pinNumber);
    if (actualPin == -1) {
      Serial.println("Invalid pin number " + String(pinNumber) + " for room " + roomName);
      continue;
    }
    
    roomToPinMapping[roomName] = actualPin;
    Serial.println("Mapped room '" + roomName + "' to pin " + String(pinNumber) + " (GPIO " + String(actualPin) + ")");
  }
  
  Serial.println("Room mapping updated with " + String(roomToPinMapping.size()) + " rooms");
  return true;
}

int getRelayPinForRoom(const String& roomName) {
  auto it = roomToPinMapping.find(roomName);
  if (it != roomToPinMapping.end()) {
    return it->second;
  }
  return -1; // Room not found
}

String getCurrentTimestamp() {
  timeClient.update();
  unsigned long epochTime = timeClient.getEpochTime();
  
  // Convert epoch time to readable format
  struct tm *ptm = gmtime((time_t *)&epochTime);
  
  char timestamp[25];
  sprintf(timestamp, "%04d-%02d-%02d %02d:%02d:%02d",
          ptm->tm_year + 1900, ptm->tm_mon + 1, ptm->tm_mday,
          ptm->tm_hour, ptm->tm_min, ptm->tm_sec);
  
  return String(timestamp);
}

void publishRelayState(const String& roomName, const String& state) {
  JsonDocument doc;
  doc["state"] = state;
  doc["timestamp"] = getCurrentTimestamp();
  
  String jsonOutput;
  serializeJson(doc, jsonOutput);
  
  mqttClient->publish((mqtt_Data_Topic + roomName).c_str(), jsonOutput.c_str(), true, 1);
}

void mqttCallback(String &topic, String &payload) {
    Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

    // Handle room configuration updates
    if (topic == mqtt_config_Topic) {
      Serial.println("Received room configuration update");
      if (updateRoomMapping(payload)) {
        Serial.println("Room mapping configuration updated successfully");
      } else {
        Serial.println("Failed to update room mapping configuration");
      }
      return;
    }

    // Handle heating commands with dynamic room mapping
    if (topic.startsWith("commands/Heating/")) {
      String roomName = topic.substring(String("commands/Heating/").length());
      int relayPin = getRelayPinForRoom(roomName);
      
      if (relayPin != -1) {
          String payloadUpper = payload;
          payloadUpper.toUpperCase();
          if (payloadUpper == "ON") {
              digitalWrite(relayPin, LOW); 
              Serial.println("Relay for room '" + roomName + "' turned ON (GPIO " + String(relayPin) + ")");
              publishRelayState(roomName, "ON");
          } else if (payloadUpper == "OFF") {
              digitalWrite(relayPin, HIGH);
              Serial.println("Relay for room '" + roomName + "' turned OFF (GPIO " + String(relayPin) + ")");
              publishRelayState(roomName, "OFF");
          } else {
              Serial.println("Invalid payload for relay command. Use 'ON' or 'OFF'.");
          }
      } else {
          Serial.println("Room '" + roomName + "' not found in configuration. Available rooms:");
          for (const auto& mapping : roomToPinMapping) {
            Serial.println("  - " + mapping.first);
          }
      }
      return;
    }
    if (topic == mqtt_SensornameTopic) {
      sensorName = payload;
      location = payload;
      location.replace("Relaismodule_", "");
      Serial.println("Sensor name set to: " + sensorName);
      Serial.println("Location set to: " + location);
      mqttClient->publish(("meta/" + sensorName + "/version/RelaisModule").c_str(), String(version), true, 0);
      mqtt_config_Topic = mqtt_config_Base + sensorName + "/Relais";
      mqtt_Data_Topic = "daten/Heizung/" + location + "/FussbodenHeizungSteuerung";
      Serial.println("Config topic set to: " + mqtt_config_Topic);
      
      //mqttClient->subscribe(mqtt_config_Topic);
      Serial.println("Subscribed to config topic: " + mqtt_config_Topic);
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
  
      String updateVersion = AzureOTAUpdater::ExtractVersionFromUrl(payload);
      Serial.println("Current firmware version is " + String(version));
      Serial.println("New firmware version is " + updateVersion);
      if(strcmp(version, updateVersion.c_str())) {
          // Trigger OTA Update
          const char *firmwareUrl = payload.c_str();
          Serial.println("New firmware available, starting OTA Update from " + String(firmwareUrl));
          otaInProgress = true;
          bool result =  AzureOTAUpdater::UpdateFirmwareFromUrl(firmwareUrl);
          if (result) {
            Serial.println("OTA Update successful initiated, waiting to be finished");
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
    Serial.println("WiFi not connected, trying to reconnect...");
    wifiLib.connect();
  }
  // Subscribing only to topic for OTA update and reading sensor name
  // Other topics (Configuration, Commands) will be subscribed once sensor name is known
  mqttClient->connect({mqtt_SensornameTopic, mqtt_OTAtopic});
  Serial.println("MQTT Client is connected");
}

void setup() {
  Serial.begin(115200);

  Serial.print("Relais Module Version:");
  Serial.println(version);
  Serial.println("-------------------------------------------------------");

  uint8_t mac[6];
  esp_read_mac(mac, ESP_MAC_WIFI_STA);

  chipID = "";
  for (int i = 0; i < 6; ++i) {
    if (mac[i] < 0x10) chipID += "0";  // Add leading zero if needed
    chipID += String(mac[i], HEX);
  }

  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);

  mqtt_SensornameTopic += chipID;

  pinMode(RELAIS_1, OUTPUT);
  pinMode(RELAIS_2, OUTPUT);
  pinMode(RELAIS_3, OUTPUT);
  pinMode(RELAIS_4, OUTPUT);
  pinMode(RELAIS_5, OUTPUT);
  pinMode(RELAIS_6, OUTPUT);
  pinMode(RELAIS_7, OUTPUT);
  pinMode(RELAIS_8, OUTPUT);

  // Connect to WiFi
  Serial.print("Connecting to WiFi ");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();
  String ssid = wifiLib.getSSID();
  
  // Initialize NTP Client
  Serial.println("Starting NTP client...");
  timeClient.begin();
  timeClient.update();
  Serial.println("NTP time synchronized: " + getCurrentTimestamp());

  // Set up MQTT
  String mqttClientID = "ESP32RelaismoduleClient_" + chipID;
  mqttClient = std::unique_ptr<MQTTClientLib>(new MQTTClientLib(mqtt_broker, mqttClientID, wifiClient, mqttCallback));
  connectToMQTT();

  // Print the IP address
  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  digitalWrite(RELAIS_1, LOW);
  delay(100);
  digitalWrite(RELAIS_2, LOW);
  delay(100);
  digitalWrite(RELAIS_3, LOW);
  delay(100);
  digitalWrite(RELAIS_4, LOW);
  delay(100);
  digitalWrite(RELAIS_5, LOW);
  delay(100);
  digitalWrite(RELAIS_6, LOW);
  delay(100);
  digitalWrite(RELAIS_7, LOW);
  delay(100);
  digitalWrite(RELAIS_8, LOW);
  delay(100);
  digitalWrite(RELAIS_1, HIGH);
  delay(100);
  digitalWrite(RELAIS_2, HIGH);
  delay(100);
  digitalWrite(RELAIS_3, HIGH);
  delay(100);
  digitalWrite(RELAIS_4, HIGH);
  delay(100);
  digitalWrite(RELAIS_5, HIGH);
  delay(100);
  digitalWrite(RELAIS_6, HIGH);
  delay(100);
  digitalWrite(RELAIS_7, HIGH);
  delay(100);
  digitalWrite(RELAIS_8, HIGH);
  delay(100);
}

void loop() {
    otaInProgress = AzureOTAUpdater::CheckUpdateStatus();
    if (otaInProgress) { delay (500); return; }

    // Update NTP time client
    timeClient.update();

    if(!mqttClient->loop())
    {
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT();
    }

    if (sensorName != "" && !subscribedToConfig) {
      mqttClient->subscribe(mqtt_config_Topic);
      Serial.println("Subscribed to config topic: " + mqtt_config_Topic);
      subscribedToConfig = true;
    }

    if (!roomToPinMapping.empty() && !subscribedToCommands) {
      mqttClient->subscribe(mqtt_CommandsTopic);
      Serial.println("Subscribed to commands topic: " + mqtt_CommandsTopic);
      subscribedToCommands = true;
    }

    delay(5000);
}

