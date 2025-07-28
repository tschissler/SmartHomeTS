// Default libraries
#include <Arduino.h>
#include <WiFi.h>
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>
#include "ESP32Helpers.h"

// Shared libaries
#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"

// Project specific libraries
#include <SPI.h>
#include <Wire.h>
#include "Adafruit_SHTC3.h"
#include <HCSR04.h>
#include "colors.h"

const char* version = FIRMWARE_VERSION;
String chipID = "";
String appName = "KellerDevice";

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_SSID and WIFI_PASSWORD as environment variables on your dev-system
WifiLib wifiLib(WIFI_PASSWORDS);
WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP);
MQTTClientLib* mqttClientLib = nullptr;

static int otaInProgress = 0;
static bool otaEnable = OTA_ENABLED != "false";
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;
static int lastMQTTSentMinute = 0;

static String baseTopic = "daten";
static String sensorName = "";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/KellerDevice";
static String mqtt_SensorNameTopic = "config/KellerDevice/{ID}/Sensorname";
static String mqtt_BrightnessTopic = "config/KellerDevice/{ID}/Brightness";
static int brightness = 255;
static int blinkCount = 0;
static const int MAX_BLINK_COUNT = 20;

const int Red_LED_Pin = 13;
const int Green_LED_Pin = 12;
const int Blue_LED_Pin = 27;
#define BLINK_DURATION 100       // Blink duration in milliseconds

const int I2CDataPin = 32;
const int I2CClockPin = 33;
const int DistanceSensor_Trigger_Pin = 15;
const int DistanceSensor_Echo_Pin = 2;

// Configuration for data collection
static const int MAX_READINGS = 24;  // 2.5 seconds * 24 = 60 seconds (1 minute)
static const unsigned long READING_INTERVAL = 2500;  // 5 seconds between readings
static unsigned long lastReadingTime = 0;

struct SensorData {
  float temperature;
  float humidity;
  float cisterneFillLevel; 
  unsigned long timestamp;
};

static SensorData readings[MAX_READINGS];
static int readingCount = 0;

Adafruit_SHTC3 shtc3 = Adafruit_SHTC3();
UltraSonicDistanceSensor distanceSensor(DistanceSensor_Trigger_Pin, DistanceSensor_Echo_Pin, 200, 20000);

void setLedColor(uint8_t r, uint8_t g, uint8_t b) {
  analogWrite(Red_LED_Pin, r*brightness/255);
  analogWrite(Green_LED_Pin, g*brightness/255);
  analogWrite(Blue_LED_Pin, b*brightness/255);
}

void setLedColor(uint8_t r, uint8_t g, uint8_t b, uint8_t overrideBrightness) {
  analogWrite(Red_LED_Pin, r*overrideBrightness/255);
  analogWrite(Green_LED_Pin, g*overrideBrightness/255);
  analogWrite(Blue_LED_Pin, b*overrideBrightness/255);
}

void blinkLed(uint8_t r, uint8_t g, uint8_t b) {
  setLedColor(r, g, b);
  delay(BLINK_DURATION);
  setLedColor(0, 0, 0);
}

void blinkLed(Color color, bool fullBrightness = false) {
  if (fullBrightness) {
    brightness = 255; 
  }
  blinkLed(color.r, color.g, color.b);
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

    if (topic == mqtt_SensorNameTopic) {
      sensorName = payload;
      Serial.println("Sensor name set to: " + sensorName);
      return;
    } 

    if (topic == mqtt_BrightnessTopic) {
      brightness = payload.toInt();
      if (brightness >= 0 && brightness <= 255) {
        Serial.println("Brightness set to: " + String(brightness));
      } else {
        Serial.println("Invalid brightness value: " + payload);
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

      setLedColor(255, 255, 0, 255); // Set LED color to yellow indicating OTA update
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
  mqttClientLib->connect({mqtt_SensorNameTopic, mqtt_BrightnessTopic, mqtt_OTAtopic});
  Serial.println("MQTT Client is connected");
}

void readSensorData() {
  if (sensorName == "") {
    Serial.println("Sensor name not set, skipping sensor reading");
    blinkLed(RED, true);
    return;
  }

  SensorData data = {NAN, NAN, NAN, 0};

  if (readingCount <= MAX_READINGS) {
    sensors_event_t humidity, temp;
    shtc3.getEvent(&humidity, &temp);
    data.humidity = humidity.relative_humidity;
    data.temperature = temp.temperature;

    data.cisterneFillLevel = 100 - distanceSensor.measureDistanceCm();

    if (isnan(data.humidity) || isnan(data.temperature)) {
      Serial.println("Failed to read from SHTC3 sensor!");
      return;
    }
    data.timestamp = millis();
    readings[readingCount] = data;
    Serial.println("Sensor data read: " + String(data.temperature) + "°C, " + String(data.humidity) + "% " + String(data.cisterneFillLevel) + "%");
    lastReadingTime = data.timestamp;
    readingCount++;
    if (blinkCount < MAX_BLINK_COUNT) {
      blinkCount++;
      blinkLed(BLUE, true);
    }    
    else {
      blinkLed(BLUE);
    }
  }
  else {
    Serial.println("Maximum readings reached, skipping sensor reading");
    blinkLed(RED, true);
    return;
  }
}

void publishSensorData()
{
  if (readingCount == 0) {
    Serial.println("No sensor data to publish");
    return;
  }

  char tempString[8];
  char humString[8];
  char cisternFillString[8];

  // Calculate average temperature and humidity
  float avgTemperature = 0;
  float avgHumidity = 0;
  float avgCisternFillLevel = 0;
  for (int i = 0; i < readingCount; i++) {
    avgTemperature += readings[i].temperature;
    avgHumidity += readings[i].humidity;
    avgCisternFillLevel += readings[i].cisterneFillLevel;
  }
  avgTemperature /= readingCount;
  avgHumidity /= readingCount;
  avgCisternFillLevel /= readingCount;
  readingCount = 0; // Reset reading count after publishing

  dtostrf(avgTemperature, 1, 2, tempString);
  dtostrf(avgHumidity, 1, 2, humString);
  dtostrf(avgCisternFillLevel, 1, 2, cisternFillString);

  if (sendMQTTMessages)
  {
    mqttSuccess = mqttClientLib->publish((baseTopic + "/temperatur/" + sensorName).c_str(), String(tempString), true, 2);
    mqttSuccess ? blinkLed(GREEN) : blinkLed(RED, true);
    mqttClientLib->publish((baseTopic + "/luftfeuchtigkeit/" + sensorName).c_str(), String(humString), true, 2);
    mqttClientLib->publish((baseTopic + "/zisterneFuellstand/" + sensorName).c_str(), String(cisternFillString), true, 2);
  }
  Serial.println("Temperature: " + String(avgTemperature) + "°C, Humidity: " + String(avgHumidity) + "%, Cistern Fill Level: " + String(avgCisternFillLevel) + "%, Version: " + version);
}

void setup() {
  Serial.begin(115200);
  Serial.println(String(appName) + " " + String(version));  
  
  // Set LED pins as output
  pinMode(Red_LED_Pin, OUTPUT);
  pinMode(Green_LED_Pin, OUTPUT);
  pinMode(Blue_LED_Pin, OUTPUT);

  // Turn off all LEDs, turn on blue LED to indicate connecting to WiFi
  setLedColor(0, 0, 255);

  chipID = ESP32Helpers::getChipId();
  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);
  mqtt_SensorNameTopic.replace("{ID}", chipID);
  mqtt_BrightnessTopic.replace("{ID}", chipID);

  // Connect to WiFi
  Serial.print("Connecting to WiFi ");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();
  String ssid = wifiLib.getSSID();

  // Set up MQTT
  String mqttClientID = "ESP32TemperatureSensorClient_" + chipID;
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT();

  timeClient.begin();
  timeClient.setTimeOffset(0); // Set your time offset from UTC in seconds
  timeClient.update();
  
  // Print the IP address
  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  Serial.println("Connecting SHTC3");
  Wire.begin(I2CDataPin, I2CClockPin);
  if (! shtc3.begin(&Wire)) {
    Serial.println("Couldn't find SHTC3");
    setLedColor(255, 0, 0, 255); 
    while (1) delay(1);
  }
  Serial.println("Found SHTC3 sensor");

  setLedColor(0, 255, 0, 255);

  mqttClientLib->publish(("meta/" + sensorName + "/version/KellerDevice").c_str(), String(version), true, 2);
}

void loop() {
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();

  if (otaInProgress < 0) {
    blinkLed(RED, true);
  }

  if (otaInProgress != 1) {
    timeClient.update();

    // Read sensor data every 5 seconds
    if (millis() - lastReadingTime >= READING_INTERVAL) {
      readSensorData();
    }

    // Transmit data every minute
    if (readingCount >= MAX_READINGS) {
      publishSensorData();
    }

    if(!mqttClientLib->loop())
    {
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT();
    }
  }
  delay(100);
}
