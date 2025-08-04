// Default libraries
#include <Arduino.h>
#include <WiFi.h>
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>
#include "ESP32Helpers.h"
#include <ArduinoJson.h>
using ArduinoJson::JsonDocument;

// Shared libaries
#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"

// Project specific libraries
#include "temperature_display.h"
#include "thermostat_data.h"

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);
WiFiClient wifiClient;
WiFiUDP ntpUDP;
static int otaInProgress = 0;
static bool otaEnable = OTA_ENABLED != "false";
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;
static String baseTopic = "daten";
static String deviceName = "";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/TemperaturDisplay";
static String mqtt_DeviceNameTopic = "config/TemperaturDisplay/{ID}/DeviceName";
static String mqtt_OutsideTempTopic = "daten/temperatur/Aussen";
static String mqtt_ThermostatWohnzimmerTopic = "data/thermostat/M3/shelly/Wohnzimmer";
static String mqtt_ThermostatEsszimmerTopic = "data/thermostat/M3/shelly/Esszimmer";
static String mqtt_ThermostatKuecheTopic = "data/thermostat/M3/shelly/Kueche";
static String mqtt_ThermostatGaestezimmerTopic = "data/thermostat/M3/shelly/Gaestezimmer";
static String mqtt_ThermostatBueroTopic = "data/thermostat/M3/shelly/Buero";

const char* version = FIRMWARE_VERSION;
String chipID = "";
NTPClient timeClient(ntpUDP);
MQTTClientLib* mqttClientLib = nullptr;

// Create display instance
TemperatureDisplay display;

// Callback functions for display events
void onTemperatureChanged(float temperature, Room room) {
  Serial.printf("Temperature changed to %.1f°C in %s\n", 
                temperature, display.roomToString(room));
  // Here you could send the temperature change to a server, MQTT, etc.
}

void onRoomChanged(Room oldRoom, Room newRoom) {
  Serial.printf("Room changed from %s to %s\n", 
                display.roomToString(oldRoom), display.roomToString(newRoom));
  // Here you could load room-specific settings, update server, etc.
}

String extractVersionFromUrl(String url) {
    int lastUnderscoreIndex = url.lastIndexOf('_');
    int lastDotIndex = url.lastIndexOf('.');

    if (lastUnderscoreIndex != -1 && lastDotIndex != -1 && lastDotIndex > lastUnderscoreIndex) {
        return url.substring(lastUnderscoreIndex + 1, lastDotIndex);
    }

    return "";
}

void mqttCallback(String &topic, String &payload) {
    Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

    if (topic == mqtt_DeviceNameTopic) {
      deviceName = payload;
      Serial.println("Sensor name set to: " + deviceName);
      return;
    } 

    if (topic == mqtt_OutsideTempTopic) {
      float outsideTemp = payload.toFloat();
      display.updateOutsideTemperature(outsideTemp);
      return;
    }
    
    if (topic == mqtt_ThermostatWohnzimmerTopic) {
      ThermostatData thermostatData;
      if (thermostatData.parseFromJson(payload)) {
          // Pass the full thermostat data object
          display.updateRoomData(thermostatData, Room::Wohnzimmer);
      } else {
          Serial.println("Failed to parse thermostat data for Wohnzimmer");
      }
      return;
    }

    if (topic == mqtt_ThermostatEsszimmerTopic) {
      ThermostatData thermostatData;
      if (thermostatData.parseFromJson(payload)) {
          display.updateRoomData(thermostatData, Room::Esszimmer);
      } else {
          Serial.println("Failed to parse thermostat data for Esszimmer");
      }
      return;
    }

    if (topic == mqtt_ThermostatKuecheTopic) {
      ThermostatData thermostatData;
      if (thermostatData.parseFromJson(payload)) {
          display.updateRoomData(thermostatData, Room::Kueche);
      } else {
          Serial.println("Failed to parse thermostat data for Küche");
      }
      return;
    }

    if (topic == mqtt_ThermostatGaestezimmerTopic) {
      ThermostatData thermostatData;
      if (thermostatData.parseFromJson(payload)) {
          display.updateRoomData(thermostatData, Room::Gaestezimmer);
      } else {
          Serial.println("Failed to parse thermostat data for Gaestezimmer");
      }
      return;
    }

    if (topic == mqtt_ThermostatBueroTopic) {
      ThermostatData thermostatData;
      if (thermostatData.parseFromJson(payload)) {
          display.updateRoomData(thermostatData, Room::Buero);
      } else {
          Serial.println("Failed to parse thermostat data for Buero");
      }
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
    wifiLib.connect();
  }
  mqttClientLib->connect({
    mqtt_DeviceNameTopic, 
    mqtt_OTAtopic, 
    mqtt_ThermostatBueroTopic, 
    mqtt_ThermostatEsszimmerTopic, 
    mqtt_ThermostatKuecheTopic, 
    mqtt_ThermostatWohnzimmerTopic, 
    mqtt_ThermostatGaestezimmerTopic});
  Serial.println("MQTT Client is connected");
}

void targetTemperatureSet(float temperature, Room room) {
  Serial.printf("Target temperature changed to %.1f°C in %s\n", 
                temperature, display.roomToString(room));
  
  // Publish the new target temperature to MQTT
  String topic = "commands/shelly/M3/" + String(display.roomToString(room));
  mqttClientLib->publish(topic, String(temperature), true, 2);
}

void setup()
{
  Serial.begin(115200);
  Serial.println("------------------  Temperature Display Firmware  ------------------");
  Serial.print("Version: ");
  Serial.println(version);
  
  chipID = ESP32Helpers::getChipId();
  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);

    // Connect to WiFi
  Serial.print("Connecting to WiFi ");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();
  String ssid = wifiLib.getSSID();

  // Set up MQTT
  String mqttClientID = "ESP32TemperatureDisplayClient_" + chipID;
  mqtt_DeviceNameTopic.replace("{ID}", chipID);
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT();

  timeClient.begin();
  timeClient.setTimeOffset(0); // Set your time offset from UTC in seconds
  timeClient.update();

  // Initialize display
  if (!display.init()) {
    Serial.println("Failed to initialize display");
    return;
  }
  
  if (!display.begin()) {
    Serial.println("Failed to start display");
    return;
  }
  
  // Setup UI
  display.setupUI();
  
  // Set callback functions
  display.setTemperatureChangeCallback(onTemperatureChanged);
  display.setRoomChangeCallback(onRoomChanged);
  display.updateIsConnected(WiFi.status() == WL_CONNECTED);
  display.setTemperatureSetCallback(targetTemperatureSet);

  mqttClientLib->publish(("meta/" + deviceName + "/version/TemperatureDisplay").c_str(), String(version), true, 2);

  Serial.println("Initialization complete");
}

void loop()
{
  display.lock();
  display.updateTime(timeClient.getEpochTime());
  display.unlock();

  if(!mqttClientLib->loop())
  {
    Serial.println("MQTT Client not connected, reconnecting in loop...");
    connectToMQTT();
  }

  Serial.println("Loop iteration complete");
  delay(1000);
}
