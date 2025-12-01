// Default libraries
#include <Arduino.h>
#include <WiFi.h>
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>
#include <ArduinoJson.h>
#include <memory>
#include <Adafruit_NeoPixel.h>

// Shared libaries
#include "ESP32Helpers.h"
#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"

// Project specific libraries
#include "colors.h"
#include "SensorType.h"
#include "SensorData.h"
#include "ISensor.h"
#include "DhtSensor.h"
#include "Sht45Sensor.h"

// Pin configuration
#define NEOPIXEL_PIN 17       // WS2812 connected to GP8
#define NUMPIXELS    1       // Number of LEDs (just one)
#define BLINK_DURATION 100       // Blink duration in milliseconds

// Sensor configuration
SensorType sensorType;
constexpr SensorType candidates[] = {
    SensorType::DHT22,
    SensorType::SHT45,
    SensorType::DS18B20
};

const char* version = TEMPSENSORFW_VERSION;
String chipID = "";
 
// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);
WiFiClient wifiClient;
WiFiUDP ntpUDP;

NTPClient timeClient(ntpUDP);
MQTTClientLib* mqttClientLib = nullptr;
Adafruit_NeoPixel pixels(NUMPIXELS, NEOPIXEL_PIN, NEO_RGB + NEO_KHZ800);
static std::unique_ptr<ISensor> sensor;

static int otaInProgress = 0;
static bool otaEnable = OTA_ENABLED != "false";
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;
static int lastMQTTSentMinute = 0;

// Configuration for data collection
static const int MAX_READINGS = 24;  // 2.5 seconds * 24 = 60 seconds (1 minute)
static const unsigned long READING_INTERVAL = 2500;  // 5 seconds between readings
static unsigned long lastReadingTime = 0;

static SensorData readings[MAX_READINGS];
static int readingCount = 0;

static String baseTopic = "daten";
static String sensorName = "";
static String location = "unknown";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/TemperaturSensor2";
static String mqtt_ConfigTopic = "config/TemperaturSensor2/{ID}";
static int brightness = 255;
static int blinkCount = 0;
static const int MAX_BLINK_COUNT = 3;

void setLedColor(uint8_t r, uint8_t g, uint8_t b) {
  pixels.setPixelColor(0, pixels.Color(r, g, b));
  pixels.show();
}

void blinkLed(uint8_t r, uint8_t g, uint8_t b) {
  setLedColor(r, g, b);
  delay(BLINK_DURATION);
  setLedColor(0, 0, 0);
}

void blinkLed(Color color, bool fullBrightness = false) {
  if (fullBrightness) {
    pixels.setBrightness(255);
  }
  else {
    pixels.setBrightness(brightness);
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

void parseConfigJSON(String jsonPayload) {
  JsonDocument doc;
  
  DeserializationError error = deserializeJson(doc, jsonPayload);
  
  if (error) {
    Serial.print("JSON parsing failed: ");
    Serial.println(error.c_str());
    return;
  }
  
  if (!doc["SensorName"].isNull()) {
    sensorName = doc["SensorName"].as<String>();
    Serial.println("Sensor name set to: " + sensorName);
  }
  
  if (!doc["Location"].isNull()) {
    location = doc["Location"].as<String>();
    Serial.println("Location set to: " + location);
  }
  
  if (!doc["Brightness"].isNull()) {
    int newBrightness = doc["Brightness"];
    if (newBrightness >= 0 && newBrightness <= 255) {
      brightness = newBrightness;
      pixels.setBrightness(brightness);
      pixels.show();
      Serial.println("Brightness set to: " + String(brightness));
    } else {
      Serial.println("Invalid brightness value: " + String(newBrightness));
    }
  }
}

void mqttCallback(String &topic, String &payload) {
    Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

    if (topic == mqtt_ConfigTopic) {
      parseConfigJSON(payload);       
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

      setLedColor(255, 255, 0); // Set LED color to yellow indicating OTA update
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

void connectToMQTT(bool cleanSession) {
  Serial.print("WiFi Status: ");
  Serial.println(WiFi.status() == WL_CONNECTED ? "Connected" : "Disconnected");

  if (WiFi.status() != WL_CONNECTED) {
      Serial.println("WiFi not connected, attempting to reconnect...");
      wifiLib.connect();
  }

  mqttClientLib->connect(cleanSession);
  mqttClientLib->subscribe({mqtt_ConfigTopic, mqtt_OTAtopic});
  Serial.println("### MQTT Client is connected and subscribed to topics");
  Serial.println("Config Topic: " + mqtt_ConfigTopic);
  Serial.println("OTA Topic: " + mqtt_OTAtopic);
}

bool tryInitializeSensor(SensorType type) {
    std::unique_ptr<ISensor> candidate;

    switch (type) {
        case SensorType::DHT22:
            candidate.reset(new DhtSensor(DHTPIN, DHTTYPE));
            break;
        case SensorType::SHT45:
            candidate.reset(new Sht45Sensor());
            break;
        // ...
        default:
            return false;
    }

    if (!candidate || !candidate->begin() || !candidate->read().success) {
        Serial.println("Probe failed for " + String(toString(type)));
        return false;
    }

    sensorType = type;
    sensor = std::move(candidate);
    Serial.println("Using sensor type " + String(toString(sensorType)));
    return true;
}

void initializeSensor() {
  for (SensorType type : candidates) {
    if (tryInitializeSensor(type)) {
        return;
    }
  }
  Serial.println("No supported sensor could be initialized");
}

void readSensorData() {
  if (sensorName == "") {
    Serial.println("Sensor name not set, skipping sensor reading");
    blinkLed(RED, true);
    return;
  }

  if (!sensor) {
    Serial.println("Sensor not initialized, skipping sensor reading");
    blinkLed(RED, true);
    return;
  }

  if (readingCount >= MAX_READINGS) {
    Serial.println("Maximum readings reached, skipping sensor reading");
    blinkLed(RED, true);
    return;
  }

  SensorData data = sensor->read();

  if (!data.success) {
    Serial.println("Failed to read from sensor type " + String(toString(sensorType)) + "!");
    blinkLed(RED, true);
    return;
  }

  readings[readingCount] = data;
  Serial.println("Sensor data read: " + String(data.temperature) + "°C, " + String(data.humidity) + "%");
  lastReadingTime = data.timestampMs;
  readingCount++;
  if (blinkCount < MAX_BLINK_COUNT) {
    blinkCount++;
    blinkLed(BLUE, true);
  }
  else {
    blinkLed(BLUE);
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

  // Calculate average temperature and humidity
  float avgTemperature = 0;
  float avgHumidity = 0;
  for (int i = 0; i < readingCount; i++) {
    avgTemperature += readings[i].temperature;
    avgHumidity += readings[i].humidity;
  }
  avgTemperature /= readingCount;
  avgHumidity /= readingCount;
  readingCount = 0; // Reset reading count after publishing

  dtostrf(avgTemperature, 1, 2, tempString);
  dtostrf(avgHumidity, 1, 2, humString);

  if (sendMQTTMessages)
  {
    mqttSuccess = mqttClientLib->publish((baseTopic + "/temperatur/" + sensorName).c_str(), String(tempString), true, 2);
    mqttSuccess ? blinkLed(GREEN) : blinkLed(RED, true);
    mqttClientLib->publish((baseTopic + "/luftfeuchtigkeit/" + sensorName).c_str(), String(humString), true, 2);
    mqttClientLib->publish((baseTopic + "/temperatur/" + location + "/" + sensorName).c_str(), String(tempString), true, 2);
    mqttClientLib->publish((baseTopic + "/luftfeuchtigkeit/" + location + "/" + sensorName).c_str(), String(humString), true, 2);
  }
  Serial.println("Temperature: " + String(avgTemperature) + "°C, Humidity: " + String(avgHumidity) + "%, Version: " + version);
}

void setup() {
  //WRITE_PERI_REG(RTC_CNTL_BROWN_OUT_REG, 0); //disable brownout detector
  Serial.begin(115200);
  Serial.print("TemperatureSensor2 version ");
  Serial.println(version);

  // Initialize NeoPixel
  pixels.begin();
  pixels.setBrightness(100);
  pixels.clear();           // Set all pixels to 'off'
  pixels.show();            // Initialize all pixels to 'off'

  chipID = ESP32Helpers::getChipId();
  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);
  mqtt_ConfigTopic.replace("{ID}", chipID);

  // Connect to WiFi
  Serial.print("Connecting to WiFi ");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();
  String ssid = wifiLib.getSSID();

  timeClient.begin();
  timeClient.setTimeOffset(0); // Set your time offset from UTC in seconds
  timeClient.update();

  initializeSensor();

  // Set up MQTT
  String mqttClientID = "ESP32TemperatureSensor2Client_" + chipID;
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT(true);
  mqttClientLib->publish(("meta/TemperaturSensor2/" + location + "/" + sensorName + "/version").c_str(), String(version), true, 2);
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

    bool mqttConnected = mqttClientLib->loop();
    if(!mqttConnected)
    {
      // Log detailed information about the disconnection
      int lastErr = mqttClientLib->lastError();
      Serial.print("MQTT loop() returned false! Last Error Code: ");
      Serial.println(lastErr);
      Serial.print("WiFi Status: ");
      Serial.println(WiFi.status() == WL_CONNECTED ? "Connected" : "Disconnected");
      Serial.print("WiFi RSSI: ");
      Serial.println(WiFi.RSSI());
      Serial.print("Free Heap: ");
      Serial.println(ESP.getFreeHeap());
      Serial.print("Uptime: ");
      Serial.println(millis() / 1000);
      
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT(false);
    }
  }
  delay(500);
}
