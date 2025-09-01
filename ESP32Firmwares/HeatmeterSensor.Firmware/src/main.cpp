#include <Arduino.h>
#include "MBusComm.h"
#include "MBusParser.h"

#include <ESP32Ping.h>
#include <esp_mac.h> 
#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"

const char* version = FIRMWARE_VERSION;
String chipID = "";

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);

WiFiClient wifiClient;
WiFiUDP ntpUDP;
std::unique_ptr<MQTTClientLib> mqttClientLib = nullptr;

static bool otaInProgress = false;
static bool otaEnable = true;
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;

static String baseTopic = "data/heating";
static String sensorName = "";
static String location = "";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/HeatSensor";
static String mqtt_ConfigTopic = "config/HeatSensor/Sensorname/";

const int IR_RX_PIN = D5;            // IR phototransistor input
const int IR_TX_PIN = D8;            // IR LED output
const uint8_t METER_ADDRESS = 0xFE;     // M-Bus primary address (0 = default)
const unsigned long READ_INTERVAL_MS = 10 * 60 * 1000;  // Read interval (e.g. 10 minutes)
unsigned long lastReadTime = -10000000;


void mqttCallback(String &topic, String &payload) {
    Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

    if (topic == mqtt_ConfigTopic) {
      sensorName = payload;
      location = payload;
      location.replace("Heatmeter_", "");
      Serial.println("Sensor name set to: " + sensorName);
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
    wifiLib.connect();
  }
  mqttClientLib->connect({mqtt_ConfigTopic, mqtt_OTAtopic});
  Serial.println("MQTT Client is connected");
}

void setup() {
  Serial.begin(115200);
  Serial.print("Heatmeter Sensor Version:");
  Serial.println(version);
  Serial.println("-------------------------------------------------------");

  uint8_t mac[6];
  esp_read_mac(mac, ESP_MAC_WIFI_STA);

  String chipID = "";
  for (int i = 0; i < 6; ++i) {
    if (mac[i] < 0x10) chipID += "0";  // Add leading zero if needed
    chipID += String(mac[i], HEX);
  }

  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);

  mqtt_ConfigTopic += chipID;

    // Connect to WiFi
  Serial.print("Connecting to WiFi ");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();
  String ssid = wifiLib.getSSID();
  
  // Set up MQTT
  String mqttClientID = "ESP32SMLSensorClient_" + chipID;
  mqttClientLib = std::make_unique<MQTTClientLib>(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT();
  
  // Print the IP address
  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  Serial.println("Initializing M-Bus interface...");
  if (!MBusInit(IR_RX_PIN, IR_TX_PIN, 2400, true)) {
      Serial.println("Error initializing M-Bus interface.");
  } else {
      Serial.println("M-Bus interface ready.");
  }
  mqttClientLib->publish(("meta/" + sensorName + "/version").c_str(), String(version), true, 2);
}

void loop() {
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();
  if (otaInProgress) { delay (500); return; }

  if(!mqttClientLib->loop())
    {
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT();
    }

  if (millis() - lastReadTime >= READ_INTERVAL_MS) {
    lastReadTime = millis();
    Serial.println();
    Serial.println("Start reading meter data...");
    // Sending signal to wake-up meter and waiting for acknowledging
    bool ack = MBusSendWakeUp(METER_ADDRESS);

    if (!ack) {
      Serial.println("Meter did not respond to wake-up. Please check connection and/or address.");
      return;
    }
    // Sending request and reading response
    MBusSendRequest(METER_ADDRESS);
    uint8_t frameBuf[300];
    int frameLen = MBusReadResponse(frameBuf, sizeof(frameBuf));
    if (frameLen < 0) {
      Serial.print("Error reading frame, code ");
      Serial.println(frameLen);
    } else {
      // Parsing frame and data output
      parseMBusFrame(frameBuf, frameLen);
    }
  }
}
