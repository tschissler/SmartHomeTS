#include <Arduino.h>
#include <WiFi.h>
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>
#include "DHT.h"

#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"
#include "ESP32Helpers.h"
#include <Adafruit_NeoPixel.h>
#include "colors.h"
#include "soc/soc.h"

// Pin configuration
#define NEOPIXEL_PIN 17       // WS2812 connected to GP8
#define NUMPIXELS    1       // Number of LEDs (just one)
#define DHTPIN 19 
#define DHTTYPE DHT22  
#define BLINK_DURATION 100       // Blink duration in milliseconds

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
DHT dht(DHTPIN, DHTTYPE);

static bool otaInProgress = false;
static bool otaEnable = OTA_ENABLED != "false";
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;
static int lastMQTTSentMinute = 0;

// Configuration for data collection
static const int MAX_READINGS = 24;  // 2.5 seconds * 24 = 60 seconds (1 minute)
static const unsigned long READING_INTERVAL = 2500;  // 5 seconds between readings
static unsigned long lastReadingTime = 0;

struct SensorData {
  float temperature;
  float humidity;
  unsigned long timestamp;
};

static SensorData readings[MAX_READINGS];
static int readingCount = 0;

static String baseTopic = "daten";
static String sensorName = "";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/TemperaturSensor2";
static String mqtt_SensorNameTopic = "config/TemperaturSensor2/{ID}/Sensorname/";
static String mqtt_BrightnessTopic = "config/TemperaturSensor2/{ID}/Brightness/";
static int brightness = 255;

void setLedColor(uint8_t r, uint8_t g, uint8_t b) {
  pixels.setPixelColor(0, pixels.Color(r, g, b));
  pixels.show();
}

void blinkLed(uint8_t r, uint8_t g, uint8_t b) {
  setLedColor(r, g, b);
  delay(BLINK_DURATION);
  setLedColor(0, 0, 0);
}

void blinkLed(Color color) {
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
        pixels.setBrightness(brightness);
        pixels.show();
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
    pixels.setBrightness(255);
    blinkLed(RED);
    pixels.setBrightness(brightness);
    return;
  }

  SensorData data = {NAN, NAN, 0};

  if (readingCount <= MAX_READINGS) {
    data.humidity = dht.readHumidity();
    data.temperature = dht.readTemperature();

    if (isnan(data.humidity) || isnan(data.temperature)) {
      Serial.println("Failed to read from DHT22 sensor!");
      return;
    }
    data.timestamp = millis();
    readings[readingCount] = data;
    Serial.println("Sensor data read: " + String(data.temperature) + "°C, " + String(data.humidity) + "%");
    lastReadingTime = data.timestamp;
    readingCount++;
    blinkLed(BLUE);
  }
  else {
    Serial.println("Maximum readings reached, skipping sensor reading");
    blinkLed(RED);
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
    blinkLed(mqttSuccess ? GREEN : RED);
    mqttClientLib->publish((baseTopic + "/luftfeuchtigkeit/" + sensorName).c_str(), String(humString), true, 2);
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

  //Init DHT sensor
  dht.begin();
  Serial.println("DHT sensor initialized");

  mqttClientLib->publish(("meta/" + sensorName + "/version/TemperaturSensor2").c_str(), String(version), true, 2);
}

void loop() {
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();

  if (!otaInProgress) {
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
