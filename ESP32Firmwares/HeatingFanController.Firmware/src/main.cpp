#include <Arduino.h>
#include <OneWire.h>
#include <cstdio>
#include <DallasTemperature.h>
#include <ArduinoJson.h>
#include <NTPClient.h>

// Shared libaries
#include "ESP32Helpers.h"
#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"
#include "SensorData.h"
#include "ISensor.h"
#include "DS18B20Sensor.h"


const int fanPWM = 4; // GPIO pin connected to the base of the transistor
const int tachPin = 21; // GPIO pin connected to the tachometer pin of the fan (optional)
const int ds18b20Pin = 5; // GPIO pin connected to the DS18B20 sensor data pin
const int freq = 25000; // 25kHz PWM frequency (standard for 4-wire PC fans)
const int resolution = 8; // 8-bit resolution (0-255)
volatile int pulseCount = 0;

static uint32_t lastRpmSampleMs = 0; // Tracks time of last RPM calculation

void IRAM_ATTR pulseCounter() {
  pulseCount = pulseCount + 1;
}

#define DS18B20_PIN 5  // on pin 5 (a 4.7K resistor is necessary)

static std::unique_ptr<ISensor> sensor;

const char *version = FIRMWARE_VERSION;
String chipID = "";

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);
WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP);
MQTTClientLib *mqttClientLib = nullptr;

static int otaInProgress = 0;
static bool otaEnable = OTA_ENABLED != "false";
static bool debug = true;
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;
static int lastMQTTSentMinute = 0;
static int lastFanPercent = 0; 

// Configuration for data collection
static const int MAX_READINGS = 24;                 // 2.5 seconds * 24 = 60 seconds (1 minute)
static const unsigned long READING_INTERVAL = 2500; // 2.5 seconds between readings
static unsigned long lastReadingTime = 0;
static std::vector<std::vector<SensorData>> readings;

//static std::vector<std::vector<SensorData>> readings;
static String baseTopic = "daten";
static String deviceName = "Heizkörperlüfter";
static std::unordered_map<std::string, String> sensorNames;
static String location = "unknown";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/HeatingFanController";
static String mqtt_ConfigTopic = "config/HeatingFanController/{ID}";
static String mqtt_CommandTopic = "commands/Heating/{DeviceName}";

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

bool initializeSensor()
{
  sensor.reset(new DS18B20Sensor(DS18B20_PIN));
  
  if (!sensor->begin())
  {
    Serial.println("Failed to initialize DS18B20 sensor.");
    sensor.reset();
    return false;
  }

  Serial.println("DS18B20 sensor initialized successfully.");
  return true;
}

static void publishStatus()
{
  if (mqttClientLib == nullptr)
  {
    return;
  }
  if (!mqttClientLib->connected())
  {
    return;
  }

  JsonDocument doc;
  doc["uptime_s"] = millis() / 1000;
  doc["wifi_connected"] = (WiFi.status() == WL_CONNECTED);
  doc["wifi_rssi"] = WiFi.RSSI();
  doc["ip"] = WiFi.localIP().toString();
  doc["mqtt_connected"] = mqttClientLib->connected();
  doc["mqtt_last_error"] = mqttClientLib->lastError();
  doc["mqtt_return_code"] = mqttClientLib->returnCode();
  doc["readings"] = static_cast<unsigned long>(readings.size());

  String payload;
  serializeJson(doc, payload);

  mqttClientLib->publish(
      ("meta/HeatingFanController/" + location + "/" + deviceName + "/status").c_str(),
      payload,
      true,
      1,
      false);
}

void readSensorData()
{
  if (debug)
    mqttClientLib->publish(
        ("meta/HeatingFanController/" + location + "/" + deviceName + "/debug_readsensordata").c_str(),
        "Readings size: " + String(static_cast<unsigned long>(readings.size())) +
            " Device: " + deviceName +
            " SensorNames: " + String(static_cast<unsigned long>(sensorNames.size())),
        true,
        2,
        false);

  if (deviceName == "" && sensorNames.empty())
  {
    Serial.println("Sensor name not set, skipping sensor reading");
    return;
  }

  if (!sensor)
  {
    Serial.println("Sensor not initialized, skipping sensor reading");
    return;
  }

  if (readings.size() >= MAX_READINGS)
  {
    Serial.println("Maximum readings reached, skipping sensor reading");
    return;
  }

  auto sensorReadings = sensor->read();
  if (sensorReadings.empty())
  {
    Serial.println("Failed to read from sensor (no readings returned)!");
    return;
  }

  std::vector<SensorData> successfulReadings;
  for (size_t i = 0; i < sensorReadings.size(); ++i)
  {
    const auto &reading = sensorReadings[i];
    if (reading.success)
    {
      Serial.printf("Sensor reading[%u] (id: %s): %.2f°C\n",
                    static_cast<unsigned>(i),
                    sensor->formatSensorId(reading.sensorId).c_str(),
                    reading.temperature);
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
}

bool parseConfigJSON(String jsonPayload)
{
  JsonDocument doc;

  DeserializationError error = deserializeJson(doc, jsonPayload);

  if (error)
  {
    Serial.print("JSON parsing failed: ");
    Serial.println(error.c_str());
    return false;
  }

  if (!doc["Location"].isNull())
  {
    location = doc["Location"].as<String>();
    Serial.println("Location set to: " + location);
  }

  if (!doc["DeviceName"].isNull())
  {
    deviceName = doc["DeviceName"].as<String>();
    Serial.println("Device name set to: " + deviceName);
    mqttClientLib->unsubscribe({mqtt_CommandTopic});
    mqtt_CommandTopic.replace("{DeviceName}", deviceName);
    mqttClientLib->subscribe({mqtt_CommandTopic});
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
  return true;
}

void parseCommandJSON(String jsonPayload)
{
  JsonDocument doc;

  DeserializationError error = deserializeJson(doc, jsonPayload);

  if (error)
  {
    Serial.print("JSON parsing failed: ");
    Serial.println(error.c_str());
    return;
  }

  if (!doc["Fanspeed"].isNull())
  {
    // Config now sends fan speed as percent: 0% = off, 100% = full speed
    int fanSpeedPercent = doc["Fanspeed"].as<int>();
    if (fanSpeedPercent < 0)
    {
      fanSpeedPercent = 0;
    }
    if (fanSpeedPercent > 100)
    {
      fanSpeedPercent = 100;
    }

    // Hardware expects PWM where 0=full speed, 255=off
    const int pwmValue = 255 - (fanSpeedPercent * 255) / 100;
    lastFanPercent = fanSpeedPercent;
    Serial.println("Fan speed set to (percent): " + String(fanSpeedPercent) + ", PWM: " + String(pwmValue));
    ledcWrite(fanPWM, pwmValue);
  }
}

void mqttCallback(String &topic, String &payload)
{
  Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

  if (topic == mqtt_ConfigTopic)
  {
    if (parseConfigJSON(payload))
    {
      mqttClientLib->publish(
          ("meta/HeatingFanController/" + location + "/" + deviceName + "/version").c_str(),
          String(version),
          true,
          2);

      if (debug)
        publishStatus();
    }
    return;
  }

  if (topic == mqtt_CommandTopic)
  {
    parseCommandJSON(payload);
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

    String updateVersion = extractVersionFromUrl(payload);
    Serial.println("Current firmware version is " + String(version));
    Serial.println("New firmware version is " + updateVersion);
    if (strcmp(version, updateVersion.c_str()))
    {
      // Trigger OTA Update
      String firmwareUrlStr = payload;
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
    wifiLib.connect(15000);
  }

  mqttClientLib->connect(cleanSession);
  mqttClientLib->subscribe({mqtt_ConfigTopic, mqtt_OTAtopic});
  Serial.println("### MQTT Client is connected and subscribed to topics");
  Serial.println("Config Topic: " + mqtt_ConfigTopic);
  Serial.println("OTA Topic: " + mqtt_OTAtopic);
}

void readFanSpeed() 
{
  const uint32_t nowMs = millis();
  uint32_t elapsedMs = 0;
  if (lastRpmSampleMs != 0) {
    elapsedMs = nowMs - lastRpmSampleMs;
  }

  int rpm = 0;
  if (elapsedMs > 0) {
    const int pulses = pulseCount;
    rpm = static_cast<int>((static_cast<int64_t>(pulses) * 30000) / elapsedMs);
  }

  pulseCount = 0;
  lastRpmSampleMs = nowMs;
  Serial.print("Fan RPM: ");
  Serial.println(rpm);
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

void publishSensorData()
{
  if (debug)
    mqttClientLib->publish(("meta/HeatingFanController/" + location + "/" + deviceName + "/debug_readsensordata").c_str(), String(static_cast<unsigned long>(readings.size())), true, 2, false);
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
    size_t temperatureCount = 0;
    size_t humidityCount = 0;
  };
  std::unordered_map<uint64_t, Aggregate> aggregates;

  for (const auto &batch : readings)
  {
    for (const auto &reading : batch)
    {
      Aggregate &agg = aggregates[reading.sensorId];
      agg.temperatureSum += reading.temperature;
      agg.temperatureCount++;
    }
  }

  if (aggregates.empty())
  {
    Serial.println("No successful sensor data to publish");
    readings.clear();
    return;
  }

  if (!(sensorNames.size() == aggregates.size()))
  {
    Serial.println("Warning: Not all sensors have a configured name.");
    Serial.println("Sensor names configured: " + String(static_cast<unsigned long>(sensorNames.size())));
    Serial.println("Sensors detected: " + String(static_cast<unsigned long>(aggregates.size())));
    Serial.println("SensorName : '" + deviceName + "'");
  }

  DynamicJsonDocument doc(1024 + aggregates.size() * 128);
  doc["location"] = location;
  doc["device"] = deviceName;
  doc["sensorType"] = "Heizkörperlüfter";
  doc["sub_category"] = "Ist";

  JsonArray temperaturesArray = doc.createNestedArray("temperatures");
  for (const auto &entry : aggregates)
  {
    const uint64_t id = entry.first;
    const Aggregate &agg = entry.second;
    float avgTemperature = agg.temperatureSum / static_cast<float>(agg.temperatureCount);

    JsonObject tempObject = temperaturesArray.createNestedObject();
    sensorDisplayName = getSensorDisplayName(id);
    tempObject[sensorDisplayName] = avgTemperature;
  }

  JsonArray percentagesArray = doc.createNestedArray("percentages");
  JsonObject fanObject = percentagesArray.createNestedObject();
  fanObject["Fanspeed"] = lastFanPercent;

  String payload;
  serializeJson(doc, payload);

  if (sendMQTTMessages)
  {
    String jsonTopic = "daten/Heizkörperlüfter/" + deviceName;
    mqttSuccess = mqttClientLib->publish(jsonTopic.c_str(), payload, true, 2);
  }
  else
  {
    Serial.println("MQTT messages disabled, not publishing data");
  }

  Serial.println("Version: " + String(version));

  readings.clear();
}

void setup()
{
  // WRITE_PERI_REG(RTC_CNTL_BROWN_OUT_REG, 0); //disable brownout detector
  Serial.begin(115200);
  Serial.print("Heatingfan Controller version ");
  Serial.println(version);

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
  String mqttClientID = "ESP32HeatingFanControllerClient_" + chipID;
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT(true);

  if (tachPin != -1) {
    pinMode(tachPin, INPUT_PULLUP);
    attachInterrupt(digitalPinToInterrupt(tachPin), pulseCounter, FALLING);
  }
  ledcAttach(fanPWM, freq, resolution);
}

void loop(void) {
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();

  if (mqttClientLib != nullptr)
  {
    bool mqttConnected = mqttClientLib->loop();
    if (!mqttConnected)
    {
      int lastErr = mqttClientLib->lastError();
      Serial.print("MQTT loop() returned false early. Last Error Code: ");
      Serial.println(lastErr);
      Serial.println("MQTT Client not connected, reconnecting...");
      // IMPORTANT: Don't block forever here; try a few times and continue.
      mqttClientLib->tryConnect(false, 3, 1000);
      mqttClientLib->subscribe({mqtt_ConfigTopic, mqtt_OTAtopic});
    }

    // Retained status for MQTT Explorer (no Serial needed)
    static uint32_t lastStatusMs = 0;
    const uint32_t nowStatusMs = millis();
    if (nowStatusMs - lastStatusMs >= 5000)
    {
      lastStatusMs = nowStatusMs;
      publishStatus();
    }

    if (debug)
      mqttClientLib->publish(("meta/HeatingFanController/" + location + "/" + deviceName + "/debug_loop").c_str(), "Main Loop " + String(otaInProgress), true, 2, false);
  }

  if (otaInProgress != 1)
  {
    timeClient.update();
    readSensorData();
    // Transmit data every minute
    if (readings.size() >= MAX_READINGS)
    {
      publishSensorData();
    }

    readFanSpeed();

    if (WiFi.status() != WL_CONNECTED)
    {
      Serial.println("WiFi disconnected, attempting to reconnect...");
      wifiLib.connect(15000);
    }
    // MQTT loop/reconnect handled at the start of loop().
  }
  delay(1000);
}