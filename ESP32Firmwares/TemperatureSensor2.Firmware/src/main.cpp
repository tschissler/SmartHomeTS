// Default libraries
#include <Arduino.h>
#include <WiFi.h>
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>
#include <ArduinoJson.h>
#include <memory>
#include <vector>
#include <unordered_map>
#include <string>
#include <cstdio>
#include <math.h>
#include <Adafruit_NeoPixel.h>
#include <Wire.h>

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
#include "DS18B20Sensor.h"
#include "Bmp280Sensor.h"

// Pin configuration
#define NEOPIXEL_PIN 17    // WS2812 connected to GP17
#define NUMPIXELS 1        // Number of LEDs (just one)
#define BLINK_DURATION 100 // Blink duration in milliseconds

// Sensor configuration
SensorType sensorType;
constexpr SensorType candidates[] = {
    SensorType::DHT22,
    SensorType::SHT45,
  SensorType::DS18B20,
  SensorType::BMP280};

const char *version = TEMPSENSORFW_VERSION;
String chipID = "";

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);
WiFiClient wifiClient;
WiFiUDP ntpUDP;

NTPClient timeClient(ntpUDP);
MQTTClientLib *mqttClientLib = nullptr;
Adafruit_NeoPixel pixels(NUMPIXELS, NEOPIXEL_PIN, NEO_RGB + NEO_KHZ800);
static std::unique_ptr<ISensor> sensor;

static int otaInProgress = 0;
static bool otaEnable = OTA_ENABLED != "false";
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;
static int lastMQTTSentMinute = 0;

// Configuration for data collection
static const int MAX_READINGS = 24;                 // 5 seconds * 24 = 120 seconds (2 minutes)
static const unsigned long READING_INTERVAL = 5000; // 5 seconds between readings
static unsigned long lastReadingTime = 0;

static std::vector<std::vector<SensorData>> readings;
static String baseTopic = "daten";
static String sensorName = "";
static std::unordered_map<std::string, String> sensorNames;
static String location = "unknown";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/TemperaturSensor2";
static String mqtt_ConfigTopic = "config/TemperaturSensor2/{ID}";
static int brightness = 255;
static int blinkCount = 0;
static const int MAX_BLINK_COUNT = 3;

void setLedColor(uint8_t r, uint8_t g, uint8_t b)
{
  pixels.setPixelColor(0, pixels.Color(r, g, b));
  pixels.show();
}

void blinkLed(uint8_t r, uint8_t g, uint8_t b)
{
  setLedColor(r, g, b);
  delay(BLINK_DURATION);
  setLedColor(0, 0, 0);
}

void blinkLed(Color color, bool fullBrightness = false)
{
  if (fullBrightness)
  {
    pixels.setBrightness(255);
  }
  else
  {
    pixels.setBrightness(brightness);
  }
  blinkLed(color.r, color.g, color.b);
}

String extractVersionFromUrl(String url)
{
  int lastUnderscoreIndex = url.lastIndexOf('_');
  int lastDotIndex = url.lastIndexOf('.');

  if (lastUnderscoreIndex != -1 && lastDotIndex != -1 && lastDotIndex > lastUnderscoreIndex)
  {
    return url.substring(lastUnderscoreIndex + 1, lastDotIndex);
  }

  return "";
}

void parseConfigJSON(String jsonPayload)
{
  JsonDocument doc;

  DeserializationError error = deserializeJson(doc, jsonPayload);

  if (error)
  {
    Serial.print("JSON parsing failed: ");
    Serial.println(error.c_str());
    return;
  }

  if (!doc["SensorName"].isNull())
  {
    sensorName = doc["SensorName"].as<String>();
    Serial.println("Sensor name set to: " + sensorName);
  }

  if (!doc["Location"].isNull())
  {
    location = doc["Location"].as<String>();
    Serial.println("Location set to: " + location);
  }

  if (!doc["SensorNames"].isNull())
  {
    JsonVariant sensorNamesVariant = doc["SensorNames"];
    if (sensorNamesVariant.is<JsonArray>())
    {
      sensorNames.clear();
      JsonArray namesArray = sensorNamesVariant.as<JsonArray>();
      for (JsonObject nameEntry : namesArray)
      {
        for (JsonPair kv : nameEntry)
        {
          String idKey = String(kv.key().c_str());
          String displayName = kv.value().as<String>();
          idKey.trim();
          displayName.trim();
          idKey.toUpperCase();
          if (idKey.length() == 0 || displayName.length() == 0)
          {
            continue;
          }
          sensorNames[std::string(idKey.c_str())] = displayName;
          Serial.println("Sensor name override added: " + idKey + " -> " + displayName);
        }
      }
      Serial.println("Sensor names loaded: " + String(static_cast<unsigned long>(sensorNames.size())));
    }
    else
    {
      Serial.println("SensorNames in JSON is not an array; ignoring configuration.");
    }
  }

  if (!doc["Brightness"].isNull())
  {
    int newBrightness = doc["Brightness"];
    if (newBrightness >= 0 && newBrightness <= 255)
    {
      brightness = newBrightness;
      pixels.setBrightness(brightness);
      pixels.show();
      Serial.println("Brightness set to: " + String(brightness));
    }
    else
    {
      Serial.println("Invalid brightness value: " + String(newBrightness));
    }
  }
}

void mqttCallback(String &topic, String &payload)
{
  Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

  if (topic == mqtt_ConfigTopic)
  {
    parseConfigJSON(payload);
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

    pixels.setBrightness(255);
    setLedColor(255, 255, 0); // Set LED color to yellow indicating OTA update
    String updateVersion = extractVersionFromUrl(payload);
    Serial.println("Current firmware version is " + String(version));
    Serial.println("New firmware version is " + updateVersion);
    if (strcmp(version, updateVersion.c_str()))
    {
      // Trigger OTA Update
      String firmwareUrlStr = payload;
      firmwareUrlStr.replace("---board---", String(BOARDCONFIG));
      const char *firmwareUrl = firmwareUrlStr.c_str();
      Serial.println("New firmware available, starting OTA Update from " + String(firmwareUrl));
      otaInProgress = true;
      bool result = AzureOTAUpdater::UpdateFirmwareFromUrl(firmwareUrl);
      if (result)
      {
        Serial.println("OTA Update successful initiated, waiting to be finished");
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

void connectToMQTT(bool cleanSession)
{
  Serial.print("WiFi Status: ");
  Serial.println(WiFi.status() == WL_CONNECTED ? "Connected" : "Disconnected");

  if (WiFi.status() != WL_CONNECTED)
  {
    Serial.println("WiFi not connected, attempting to reconnect...");
    wifiLib.connect();
  }

  mqttClientLib->connect(cleanSession);
  mqttClientLib->subscribe({mqtt_ConfigTopic, mqtt_OTAtopic});
  Serial.println("### MQTT Client is connected and subscribed to topics");
  Serial.println("Config Topic: " + mqtt_ConfigTopic);
  Serial.println("OTA Topic: " + mqtt_OTAtopic);
}

bool tryInitializeSensor(SensorType type)
{
  // Ensure I2C is in a clean state before probing any sensor.
  // ESP32-C6 I2C driver can error if begin() is called repeatedly without end().
  Wire.end();

  std::unique_ptr<ISensor> candidate;

  switch (type)
  {
  case SensorType::DHT22:
    Serial.println("Probing for DHT22 sensor...");
    candidate.reset(new DhtSensor(DHTPIN, DHTTYPE));
    break;
  case SensorType::SHT45:
    Serial.println("Probing for SHT45 sensor...");
    candidate.reset(new Sht45Sensor());
    break;
  case SensorType::DS18B20:
    Serial.println("Probing for DS18B20 sensor...");
    candidate.reset(new DS18B20Sensor());
    break;
  case SensorType::BMP280:
    Serial.println("Probing for BMP280 sensor...");
    candidate.reset(new Bmp280Sensor());
    break;
  default:
    return false;
  }

  if (!candidate || !candidate->begin())
  {
    Serial.println("Begin probe failed for " + String(toString(type)));
    return false;
  }

  auto initialReadings = candidate->read();
  if (initialReadings.empty() || !initialReadings.front().success)
  {
    Serial.println("Read probe failed for " + String(toString(type)));
    return false;
  }

  sensorType = type;
  sensor = std::move(candidate);
  readings.clear();
  Serial.println("Using sensor type " + String(toString(sensorType)));
  return true;
}

bool initializeSensor()
{
  for (SensorType type : candidates)
  {
    if (tryInitializeSensor(type))
    {
      return true;
    }
  }
  Serial.println("No supported sensor could be initialized");
  blinkLed(RED, true);
  return false;
}

String getSensorDisplayName(uint64_t sensorId)
{
  String idKey = sensor->formatSensorId(sensorId);
  idKey.toUpperCase();
  std::string lookupKey(idKey.c_str());
  auto it = sensorNames.find(lookupKey);
  if (it != sensorNames.end()) {
    return it->second;
  }
  return idKey;
}

void readSensorData()
{
  if (sensorName == "" && sensorNames.empty())
  {
    Serial.println("Sensor name not set, skipping sensor reading");
    blinkLed(RED, true);
    return;
  }

  if (!sensor)
  {
    Serial.println("Sensor not initialized, skipping sensor reading");
    blinkLed(RED, true);
    return;
  }

  if (readings.size() >= MAX_READINGS)
  {
    Serial.println("Maximum readings reached, skipping sensor reading");
    blinkLed(RED, true);
    return;
  }

  auto sensorReadings = sensor->read();
  if (sensorReadings.empty())
  {
    Serial.println("Failed to read from sensor type " + String(toString(sensorType)) + " (no readings returned)!");
    blinkLed(RED, true);
    return;
  }

  std::vector<SensorData> successfulReadings;
  for (size_t i = 0; i < sensorReadings.size(); ++i)
  {
    const auto &reading = sensorReadings[i];
    if (reading.success)
    {
      const bool hasHumidity = !isnan(reading.humidity);
      const bool hasPressure = !isnan(reading.pressure);

      if (hasHumidity && hasPressure)
      {
        Serial.printf("Sensor reading[%u] (id: %s): %.2f°C, %.2f%%, %.2f hPa\n",
                      static_cast<unsigned>(i),
                      getSensorDisplayName(reading.sensorId).c_str(),
                      reading.temperature,
                      reading.humidity,
                      reading.pressure);
      }
      else if (hasHumidity)
      {
        Serial.printf("Sensor reading[%u] (id: %s): %.2f°C, %.2f%%\n",
                      static_cast<unsigned>(i),
                      getSensorDisplayName(reading.sensorId).c_str(),
                      reading.temperature,
                      reading.humidity);
      }
      else if (hasPressure)
      {
        Serial.printf("Sensor reading[%u] (id: %s): %.2f°C, %.2f hPa\n",
                      static_cast<unsigned>(i),
                      getSensorDisplayName(reading.sensorId).c_str(),
                      reading.temperature,
                      reading.pressure);
      }
      else
      {
        Serial.printf("Sensor reading[%u] (id: %s): %.2f°C\n",
                      static_cast<unsigned>(i),
                      getSensorDisplayName(reading.sensorId).c_str(),
                      reading.temperature);
      }
      successfulReadings.push_back(reading);
    }
    else
    {
      String formattedId = sensor->formatSensorId(reading.sensorId);
      Serial.printf("Sensor reading[%u] (id: %s): failed\n",
                    static_cast<unsigned>(i),
                    formattedId.c_str());
    }
  }

  if (!successfulReadings.empty())
  {
    readings.push_back(successfulReadings);
  }

  lastReadingTime = millis();
  if (blinkCount < MAX_BLINK_COUNT)
  {
    blinkCount++;
    blinkLed(BLUE, true);
  }
  else
  {
    blinkLed(BLUE);
  }
}

void publishSensorData()
{
  String sensorDisplayName = "";
  if (readings.empty())
  {
    Serial.println("No sensor data to publish");
    return;
  }

  if (!sensor)
  {
    Serial.println("Sensor not initialized, cannot publish data");
    return;
  }

  struct Aggregate
  {
    float temperatureSum = 0.0f;
    float humiditySum = 0.0f;
    float pressureSum = 0.0f;
    size_t temperatureCount = 0;
    size_t humidityCount = 0;
    size_t pressureCount = 0;
  };
  std::unordered_map<uint64_t, Aggregate> aggregates;

  for (const auto &batch : readings)
  {
    for (const auto &reading : batch)
    {
      Aggregate &agg = aggregates[reading.sensorId];
      agg.temperatureSum += reading.temperature;
      agg.temperatureCount++;
      if (!isnan(reading.humidity))
      {
        agg.humiditySum += reading.humidity;
        agg.humidityCount++;
      }
      if (!isnan(reading.pressure))
      {
        agg.pressureSum += reading.pressure;
        agg.pressureCount++;
      }
    }
  }

  if (aggregates.empty())
  {
    Serial.println("No successful sensor data to publish");
    readings.clear();
    return;
  }

  if (!(sensorNames.size() == aggregates.size() || (aggregates.size() == 1 && sensorName != "")))
  {
    Serial.println("Warning: Not all sensors have a configured name.");
    Serial.println("Sensor names configured: " + String(static_cast<unsigned long>(sensorNames.size())));
    Serial.println("Sensors detected: " + String(static_cast<unsigned long>(aggregates.size())));
    Serial.println("SensorName : '" + sensorName + "'");
  }

  for (const auto &entry : aggregates)
  {
    const uint64_t id = entry.first;
    const Aggregate &agg = entry.second;
    float avgTemperature = agg.temperatureSum / static_cast<float>(agg.temperatureCount);
    float avgHumidity = agg.humidityCount > 0 ? (agg.humiditySum / static_cast<float>(agg.humidityCount)) : NAN;
    float avgPressure = agg.pressureCount > 0 ? (agg.pressureSum / static_cast<float>(agg.pressureCount)) : NAN;

    const bool hasHumidity = agg.humidityCount > 0;
    const bool hasPressure = agg.pressureCount > 0;
    if (hasHumidity && hasPressure)
    {
      Serial.printf("Average sensor (id: %s): %.2f°C, %.2f%%, %.2f hPa\n",
                    getSensorDisplayName(id).c_str(),
                    avgTemperature,
                    avgHumidity,
                    avgPressure);
    }
    else if (hasHumidity)
    {
      Serial.printf("Average sensor (id: %s): %.2f°C, %.2f%%\n",
                    getSensorDisplayName(id).c_str(),
                    avgTemperature,
                    avgHumidity);
    }
    else if (hasPressure)
    {
      Serial.printf("Average sensor (id: %s): %.2f°C, %.2f hPa\n",
                    getSensorDisplayName(id).c_str(),
                    avgTemperature,
                    avgPressure);
    }
    else
    {
      Serial.printf("Average sensor (id: %s): %.2f°C\n",
                    getSensorDisplayName(id).c_str(),
                    avgTemperature);
    }

    if (sendMQTTMessages)
    {
      char tempString[8];
      char humString[8];
      char pressureString[10];

      dtostrf(avgTemperature, 1, 2, tempString);
      dtostrf(avgHumidity, 1, 2, humString);
      dtostrf(avgPressure, 1, 2, pressureString);

      if (sensorName != "")
      {
        sensorDisplayName = sensorName;
      }
      else
      {
        sensorDisplayName = getSensorDisplayName(id);
      }
      String temperatureTopic = baseTopic + "/temperatur/" + location + "/" + sensorDisplayName;
      String humidityTopic = baseTopic + "/luftfeuchtigkeit/" + location + "/" + sensorDisplayName;
      String pressureTopic = baseTopic + "/luftdruck/" + location + "/" + sensorDisplayName;

      mqttSuccess = mqttClientLib->publish(temperatureTopic.c_str(), String(tempString), true, 2);
      mqttSuccess ? blinkLed(GREEN) : blinkLed(RED, true);
      if (!isnan(avgHumidity))
      {
        mqttClientLib->publish(humidityTopic.c_str(), String(humString), true, 2);
      }
      if (!isnan(avgPressure))
      {
        mqttClientLib->publish(pressureTopic.c_str(), String(pressureString), true, 2);
      }
    }
    else
    {
      Serial.println("MQTT messages disabled, not publishing data");
    }
  }

  Serial.println("Version: " + String(version));

  readings.clear();
}

void setup()
{
  // WRITE_PERI_REG(RTC_CNTL_BROWN_OUT_REG, 0); //disable brownout detector
  Serial.begin(115200);
  Serial.print("TemperatureSensor2 version ");
  Serial.println(version);

  // Initialize NeoPixel
  pixels.begin();
  pixels.setBrightness(100);
  pixels.clear(); // Set all pixels to 'off'
  pixels.show();  // Initialize all pixels to 'off'

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

  readings.clear();
  readings.reserve(MAX_READINGS);

  initializeSensor();

  // Set up MQTT
  String mqttClientID = "ESP32TemperatureSensor2Client_" + chipID;
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT(true);
  mqttClientLib->publish(("meta/TemperaturSensor2/" + location + "/" + sensorName + "/version").c_str(), String(version), true, 2);
}

void loop()
{
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();

  if (otaInProgress < 0)
  {
    blinkLed(RED, true);
  }

  if (otaInProgress != 1)
  {
    timeClient.update();

    if (millis() - lastReadingTime >= READING_INTERVAL)
    {
      readSensorData();
    }

    if (readings.size() >= MAX_READINGS)
    {
      publishSensorData();
    }

    bool mqttConnected = mqttClientLib->loop();
    if (!mqttConnected)
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
