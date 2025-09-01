#include <Arduino.h>
#include "MBusComm.h"
#include "MBusParser.h"
#include <ArduinoJson.h>

#include <ESP32Ping.h>
#include <esp_mac.h> 
#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"
#include "LEDLib.h"

const char* version = FIRMWARE_VERSION;
String chipID = "";

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);

WiFiClient wifiClient;
WiFiUDP ntpUDP;
std::unique_ptr<MQTTClientLib> mqttClient = nullptr;

// M-Bus communication and parser instances
MBusComm mbusComm;
MBusParser mbusParser;

static bool otaInProgress = false;
static bool otaEnable = true;
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;

static String baseTopic = "data/heating";
static String sensorName = "";
static String location = "";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/HeatMeter";
static String mqtt_ConfigTopic = "config/HeatMeter/Sensorname/";

enum SystemStatus { HEALTHY, TRANSMITTING, NO_METER_RESPONSE, NO_WIFI, NO_MQTT, OTA_IN_PROGRESS };
const int IR_RX_PIN = D5;            // IR phototransistor input
const int IR_TX_PIN = D8;            // IR LED output
const int RED_LED_PIN = D7;         
const int GREEN_LED_PIN = D9;
const int BLUE_LED_PIN = D10;
const uint8_t METER_ADDRESS = 0xFE;     // M-Bus primary address (0 = default)
const unsigned long READ_INTERVAL_MS = 10 * 60 * 1000;  // Read interval (e.g. 10 minutes)
unsigned long lastReadTime = -10000000;
const unsigned long LED_BLINK_INTERVAL_MS = 3 * 1000;
unsigned long lastBlinkTime = 0;
RGBLED led = RGBLED(RED_LED_PIN, GREEN_LED_PIN, BLUE_LED_PIN);
SystemStatus state = NO_WIFI;

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
    Serial.println("WiFi not connected, trying to reconnect...");
    wifiLib.connect();
  }
  mqttClient->connect({mqtt_ConfigTopic, mqtt_OTAtopic});
  Serial.println("MQTT Client is connected");
}

void printMBusHeaderInfo(MBusHeader &header)
{
    Serial.println("M-Bus Header");
    Serial.println("-------------------------------------------");
    Serial.println("Identification number: " + String(header.id));
    Serial.println("Manufacturer: " + String(header.manufacturer.name) + " (" + String(header.manufacturer.code) + ")");
    Serial.println("Meter version: " + String(header.version));
    Serial.print("Medium: 0x");
    Serial.print(header.medium, HEX);
    Serial.println(" (" + String(MBusParser::mediumCodeToString(header.medium)) + ")");
    Serial.println("Access number: " + String(header.accessNo));
    Serial.print("Status: 0x");
    Serial.print(header.status, HEX);
    Serial.println(" => " + MBusParser::statusByteToString(header.status));
    Serial.print("Signature: 0x");
    Serial.println(header.signature, HEX);
}

void printMBusData(const MBusData &data) {
    Serial.println();
    Serial.println("M-Bus Data Records");
    Serial.println("-------------------------------------------");
    Serial.println("Device ID: " + String(data.deviceId));
    
    if (data.totalHeatEnergy.hasValue)
        Serial.println("Total Heat Energy : " + String(data.totalHeatEnergy.value) + " " + data.totalHeatEnergy.unit);
    else
        Serial.println("Total Heat Energy : Not set");
        
    if (data.currentValue.hasValue)
        Serial.println("Current Value : " + String(data.currentValue.value) + " " + data.currentValue.unit);
    else
        Serial.println("Current Value : Not set");
        
    if (data.heatmeterHeat.hasValue)
        Serial.println("Heatmeter Heat : " + String(data.heatmeterHeat.value) + " " + data.heatmeterHeat.unit);
    else
        Serial.println("Heatmeter Heat : Not set");
        
    if (data.heatCoolingmeterHeat.hasValue)
        Serial.println("Heat/Coolingmeter Heat : " + String(data.heatCoolingmeterHeat.value) + " " + data.heatCoolingmeterHeat.unit);
    else
        Serial.println("Heat/Coolingmeter Heat : Not set");
        
    if (data.totalVolume.hasValue)
        Serial.println("Total Volume : " + String(data.totalVolume.value) + " " + data.totalVolume.unit);
    else
        Serial.println("Total Volume : Not set");
        
    if (data.powerCurrentValue.hasValue)
        Serial.println("Power - Current : " + String(data.powerCurrentValue.value) + " " + data.powerCurrentValue.unit);
    else
        Serial.println("Power - Current : Not set");
        
    if (data.powerMaximumValue.hasValue)
        Serial.println("      - Maximum : " + String(data.powerMaximumValue.value) + " " + data.powerMaximumValue.unit);
    else
        Serial.println("      - Maximum : Not set");
        
    if (data.flowCurrentValue.hasValue)
        Serial.println("Flow - Current : " + String(data.flowCurrentValue.value) + " " + data.flowCurrentValue.unit);
    else
        Serial.println("Flow - Current : Not set");
        
    if (data.flowMaximumValue.hasValue)
        Serial.println("     - Maximum : " + String(data.flowMaximumValue.value) + " " + data.flowMaximumValue.unit);
    else
        Serial.println("     - Maximum : Not set");
        
    if (data.forwardFlowTemperature.hasValue)
        Serial.println("Forward Flow Temperature : " + String(data.forwardFlowTemperature.value) + " " + data.forwardFlowTemperature.unit);
    else
        Serial.println("Forward Flow Temperature : Not set");
        
    if (data.returnFlowTemperature.hasValue)
        Serial.println("Return Flow Temperature : " + String(data.returnFlowTemperature.value) + " " + data.returnFlowTemperature.unit);
    else
        Serial.println("Return Flow Temperature : Not set");
        
    if (data.temperatureDifference.hasValue)
        Serial.println("Temperature Difference : " + String(data.temperatureDifference.value) + " " + data.temperatureDifference.unit);
    else
        Serial.println("Temperature Difference : Not set");
        
    if (data.daysInOperation.hasValue)
        Serial.println("Days in operation: " + String(data.daysInOperation.value) + " " + data.daysInOperation.unit);
    else
        Serial.println("Days in operation: Not set");
        
    Serial.println("Current Date and Time: " + String((data.currentDateAndTime.day)) + "/" + String((data.currentDateAndTime.month)) + "/" + String((data.currentDateAndTime.year)) + " " + String((data.currentDateAndTime.hour)) + ":" + String((data.currentDateAndTime.minute)));
}

void publishMBusData(MBusHeader header, MBusData data) {
    JsonDocument doc;
    doc["status"] = header.status;
    doc["status_text"] = MBusParser::statusByteToString(header.status);
    doc["totalHeatEnergy"] = data.totalHeatEnergy.value;
    doc["totalHeatEnergy_text"] = String(data.totalHeatEnergy.value) + " " + data.totalHeatEnergy.unit;
    doc["totalVolume"] = data.totalVolume.value;
    doc["totalVolume_text"] = String(data.totalVolume.value) + " " + data.totalVolume.unit;
    doc["power"] = data.powerCurrentValue.value;
    doc["power_text"] = String(data.powerCurrentValue.value) + " " + data.powerCurrentValue.unit;
    doc["powerMaximum"] = data.powerMaximumValue.value;
    doc["powerMaximum_text"] = String(data.powerMaximumValue.value) + " " + data.powerMaximumValue.unit;
    doc["flow"] = data.flowCurrentValue.value;
    doc["flow_text"] = String(data.flowCurrentValue.value) + " " + data.flowCurrentValue.unit;
    doc["flowMaximum"] = data.flowMaximumValue.value;
    doc["flowMaximum_text"] = String(data.flowMaximumValue.value) + " " + data.flowMaximumValue.unit;
    doc["forwardFlowTemperature"] = data.forwardFlowTemperature.value;
    doc["forwardFlowTemperature_text"] = String(data.forwardFlowTemperature.value) + " " + data.forwardFlowTemperature.unit;
    doc["returnFlowTemperature"] = data.returnFlowTemperature.value;
    doc["returnFlowTemperature_text"] = String(data.returnFlowTemperature.value) + " " + data.returnFlowTemperature.unit;
    doc["temperatureDifference"] = data.temperatureDifference.value;
    doc["temperatureDifference_text"] = String(data.temperatureDifference.value) + " " + data.temperatureDifference.unit;
    doc["daysInOperation"] = data.daysInOperation.value;
    doc["daysInOperation_text"] = String(data.daysInOperation.value) + " " + data.daysInOperation.unit;
    doc["currentDateAndTime"] = String((data.currentDateAndTime.day)) + "/" + String((data.currentDateAndTime.month)) + "/" + String((data.currentDateAndTime.year)) + " " + String((data.currentDateAndTime.hour)) + ":" + String((data.currentDateAndTime.minute));

    char jsonBuffer[256];
    serializeJson(doc, jsonBuffer);

    mqttClient->publish(baseTopic + "/" + location, jsonBuffer, true, 2);
}

void blinkStatus()
{
  if (state == SystemStatus::HEALTHY) {
    led.blink(GREEN, 200, 1, 8);
  } else if (state == SystemStatus::NO_METER_RESPONSE) {
    led.blink(RED, 200, 2, 255);
  } else if (state == SystemStatus::NO_WIFI) {
    led.blink(RED, 200, 3, 255);
  } else if (state == SystemStatus::NO_MQTT) {
    led.blink(RED, 200, 4, 255);
  } else if (state == SystemStatus::OTA_IN_PROGRESS) {
    led.blink(YELLOW, 100, 1, 255);
  } else if (state == SystemStatus::TRANSMITTING) {
    led.blink(BLUE, 200, 2, 255);
  }
}

void setup() {
  Serial.begin(115200);
  led.blink(RED, 200, 2);
  led.blink(GREEN, 200, 2);
  led.blink(BLUE, 200, 2);
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
  String mqttClientID = "ESP32HeatmeterSensorClient_" + chipID;
  mqttClient = std::make_unique<MQTTClientLib>(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT();
  
  // Print the IP address
  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  Serial.println("Initializing M-Bus interface...");
  if (!mbusComm.init(IR_RX_PIN, IR_TX_PIN, 2400, false)) {
      Serial.println("Error initializing M-Bus interface.");
  } else {
      Serial.println("M-Bus interface ready.");
  }
  mqttClient->publish(("meta/" + sensorName + "/version").c_str(), String(version), true, 2);
  state = SystemStatus::HEALTHY;
}

void loop() {
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();
  if (otaInProgress) { state = SystemStatus::OTA_IN_PROGRESS; delay (500); return; }
  else if (state == SystemStatus::OTA_IN_PROGRESS) { state = SystemStatus::HEALTHY; }

  if(!mqttClient->loop())
    {
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT();
    }

  if (millis() - lastBlinkTime >= LED_BLINK_INTERVAL_MS) {
    lastBlinkTime = millis();
    blinkStatus();
    if (state == SystemStatus::TRANSMITTING)
      state = SystemStatus::HEALTHY;
  }

  if (millis() - lastReadTime >= READ_INTERVAL_MS) {
    lastReadTime = millis();
    Serial.println();
    Serial.println("Start reading meter data...");
    // Sending signal to wake-up meter and waiting for acknowledging
    bool ack = mbusComm.sendWakeUp(METER_ADDRESS);

    if (!ack) {
      Serial.println("Meter did not respond to wake-up. Please check connection and/or address.");
      state = SystemStatus::NO_METER_RESPONSE;
      return;
    }
    // Sending request and reading response
    mbusComm.sendRequest(METER_ADDRESS);
    uint8_t frameBuf[300];
    int frameLen = mbusComm.readResponse(frameBuf, sizeof(frameBuf));
    if (frameLen < 0) {
      Serial.print("Error reading frame, code ");
      Serial.println(frameLen);
      state = SystemStatus::NO_METER_RESPONSE;
    } else {
      // Parsing frame and data output
      MBusParser::debug = true;
      MBusParsingResult result = MBusParser::parseMBusFrame(frameBuf, frameLen);
      printMBusHeaderInfo(result.header);
      printMBusData(result.data);

      publishMBusData(result.header, result.data);
      state = SystemStatus::TRANSMITTING;
    }
  }
}
