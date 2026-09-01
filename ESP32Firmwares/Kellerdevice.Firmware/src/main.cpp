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
const String mqtt_broker = "mosquitto.intern";
const int mqtt_port = 1883;
static String mqtt_OTAtopic = "OTAUpdate/KellerDevice";
// Retained device heartbeat on status/<location>/<deviceType>/<deviceName>, so a consumer
// can tell a silent device from a healthy one. Must match the OTA topic segment.
static const uint32_t HEARTBEAT_INTERVAL_MS = 60000;
static uint32_t lastHeartbeatMs = 0;
static String mqtt_SensorNameTopic = "config/KellerDevice/{ID}/Sensorname";
static String mqtt_BrightnessTopic = "config/KellerDevice/{ID}/Brightness";
// Only known once the sensor name has arrived, so the subscription cannot be set up in setup().
static String mqtt_FillLevelTopic = "";
// The level we published before the reboot is a far better starting point for the slew rate
// limit than the "full" default. Subscribing to our own retained message gets it back. The
// network calls stay out of the MQTT callback and are done from loop() instead.
static bool initialFillLevelRestored = false;
static bool fillLevelSubscribePending = false;
static bool fillLevelUnsubscribePending = false;
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

// Cistern measurement plausibility
// The ultrasonic sensor has a blind zone of roughly 20 cm. Once the water reaches into it the
// sensor stops seeing the direct echo and locks onto detours instead - measured data shows stable
// clusters at roughly 2x, 3x and 4x the true distance, plus outright timeouts (the library
// returns -1). The error is therefore one-sided: a detour is always LONGER than the direct path,
// never shorter. That rules out the mean and the median, which both track the middle of a
// distribution and drift along with the detours. The shortest readings are the trustworthy ones.
static const float DISTANCE_BLIND_ZONE_CM = 20.0;
static const float DISTANCE_MAX_VALID_CM = 100.0;
static const float SENSOR_HEIGHT_ABOVE_FLOOR_CM = 100.0;
// Not the outright minimum: a single spurious short reading would then set the level. The third
// shortest of 24 is roughly the 10th percentile and survives a couple of those.
static const int SHORTEST_READING_INDEX = 2;
// Inside the blind zone the level is at least this high but the exact value is unknowable.
// Reporting the cap keeps the graph flat instead of letting it flutter.
static const float MAX_RELIABLE_FILL_LEVEL_PERCENT = 80.0;
// Losing the direct echo makes the level appear to collapse - 45 percentage points within a
// single minute has been measured, against a 99th percentile of 0.63 for real changes. A step
// beyond these bounds is the sensor slipping, not the water moving. Rises get more headroom than
// drops: heavy rain fills the tank faster than any draw-off empties it, and a detour echo can
// only ever make the level look lower, never higher.
static const float MAX_LEVEL_DROP_PER_CYCLE = 2.0;
static const float MAX_LEVEL_RISE_PER_CYCLE = 5.0;
// Start from "full" rather than from whatever detour echo the first cycle after a reboot picks
// up. The sensor only ever fails when the water is in its blind zone, so on a fresh boot that is
// by far the likelier state - and it is the safer error, since it holds off the pump.
static float lastValidFillLevel = MAX_RELIABLE_FILL_LEVEL_PERCENT;

struct SensorData {
  float temperature;
  float humidity;
  // Raw distance. Negative marks a timeout, NAN a reading beyond the tank floor.
  float cisternDistanceCm;
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
      mqtt_FillLevelTopic = baseTopic + "/zisterneFuellstand/M1/" + sensorName;
      if (!initialFillLevelRestored) {
        fillLevelSubscribePending = true;
      }
      return;
    }

    if (mqtt_FillLevelTopic != "" && topic == mqtt_FillLevelTopic) {
      // Our own retained level, picked up once after a reboot. Everything after that is the
      // echo of what we just published ourselves, so unsubscribe as soon as we have it.
      if (!initialFillLevelRestored) {
        float restored = payload.toFloat();
        if (payload != "nan" && restored > 0 && restored <= MAX_RELIABLE_FILL_LEVEL_PERCENT) {
          lastValidFillLevel = restored;
          Serial.println("Restored fill level from retained message: " + String(restored) + "%");
        }
        else {
          Serial.println("Retained fill level '" + payload + "' unusable, keeping default");
        }
        initialFillLevelRestored = true;
        fillLevelUnsubscribePending = true;
      }
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

void connectToMQTT(bool cleanSession = false) {
  if (WiFi.status() != WL_CONNECTED) {
    wifiLib.connect();
  }
  mqttClientLib->connect(cleanSession);
  mqttClientLib->subscribe({mqtt_SensorNameTopic, mqtt_BrightnessTopic, mqtt_OTAtopic});
  Serial.println("MQTT Client is connected");
}

// Near-shortest of this cycle's distances, since every wrong reading is a detour and therefore
// too long. Returns NAN when nothing usable came in.
float shortestDistanceCm(int &validCount) {
  float values[MAX_READINGS];
  validCount = 0;
  for (int i = 0; i < readingCount; i++) {
    float d = readings[i].cisternDistanceCm;
    if (!isnan(d) && d >= 0) {
      values[validCount++] = d;
    }
  }
  if (validCount == 0) {
    return NAN;
  }

  for (int i = 1; i < validCount; i++) {
    float key = values[i];
    int j = i - 1;
    while (j >= 0 && values[j] > key) {
      values[j + 1] = values[j];
      j--;
    }
    values[j + 1] = key;
  }

  int index = SHORTEST_READING_INDEX < validCount ? SHORTEST_READING_INDEX : validCount - 1;
  return values[index];
}

int countTimeouts() {
  int timeouts = 0;
  for (int i = 0; i < readingCount; i++) {
    float d = readings[i].cisternDistanceCm;
    if (!isnan(d) && d < 0) {
      timeouts++;
    }
  }
  return timeouts;
}

void readSensorData() {
  if (sensorName == "") {
    Serial.println("Sensor name not set, skipping sensor reading");
    blinkLed(RED, true);
    return;
  }

  SensorData data = {NAN, NAN, NAN, 0};

  if (readingCount < MAX_READINGS) {
    sensors_event_t humidity, temp;
    shtc3.getEvent(&humidity, &temp);
    data.humidity = humidity.relative_humidity;
    data.temperature = temp.temperature;

    // A negative value is the library's timeout, which means the water is too close for an echo
    // to come back - that is a "full" hint and is kept as such. Beyond the tank floor there is
    // nothing left to reflect off, so those readings are dropped.
    float distance = distanceSensor.measureDistanceCm();
    if (distance > DISTANCE_MAX_VALID_CM) {
      data.cisternDistanceCm = NAN;
      Serial.println("Discarding distance beyond tank floor: " + String(distance) + " cm");
    }
    else {
      data.cisternDistanceCm = distance;
    }

    if (isnan(data.humidity) || isnan(data.temperature)) {
      Serial.println("Failed to read from SHTC3 sensor!");
      return;
    }
    data.timestamp = millis();
    readings[readingCount] = data;
    Serial.println("Sensor data read: " + String(data.temperature) + "°C, " + String(data.humidity) + "%, distance " + String(data.cisternDistanceCm) + " cm");
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
  for (int i = 0; i < readingCount; i++) {
    avgTemperature += readings[i].temperature;
    avgHumidity += readings[i].humidity;
  }
  avgTemperature /= readingCount;
  avgHumidity /= readingCount;

  int validDistanceCount = 0;
  float distance = shortestDistanceCm(validDistanceCount);
  int timeouts = countTimeouts();
  int totalReadings = readingCount;
  readingCount = 0; // Reset reading count after publishing

  float fillLevel;
  if (timeouts > totalReadings / 2) {
    // No echo comes back at all when the water sits right under the sensor, so a run of
    // timeouts is the most reliable "full" signal this sensor produces.
    fillLevel = MAX_RELIABLE_FILL_LEVEL_PERCENT;
    Serial.println("Mostly timeouts (" + String(timeouts) + "/" + String(totalReadings) + "), reporting tank as full");
    lastValidFillLevel = fillLevel;
  }
  else if (isnan(distance)) {
    // Nothing usable came in at all. Hold the last known level rather than publishing a gap.
    Serial.println("No usable distance readings this cycle, holding last known fill level");
    fillLevel = lastValidFillLevel;
  }
  else if (distance < DISTANCE_BLIND_ZONE_CM) {
    // Saturation: the sensor reports its floor value, the water is at least that high.
    fillLevel = MAX_RELIABLE_FILL_LEVEL_PERCENT;
    Serial.println("Distance " + String(distance) + " cm is inside the blind zone, reporting tank as full");
    lastValidFillLevel = fillLevel;
  }
  else {
    float measured = SENSOR_HEIGHT_ABOVE_FLOOR_CM - distance;
    if (measured > MAX_RELIABLE_FILL_LEVEL_PERCENT) {
      measured = MAX_RELIABLE_FILL_LEVEL_PERCENT;
    }

    float change = measured - lastValidFillLevel;
    if (change < -MAX_LEVEL_DROP_PER_CYCLE || change > MAX_LEVEL_RISE_PER_CYCLE) {
      // Water cannot move this fast, so the sensor lost the direct echo. Keep what we had.
      Serial.println("Rejecting jump from " + String(lastValidFillLevel) + "% to " + String(measured) + "%, holding");
      fillLevel = lastValidFillLevel;
    }
    else {
      fillLevel = measured;
      lastValidFillLevel = fillLevel;
    }
  }

  dtostrf(avgTemperature, 1, 2, tempString);
  dtostrf(avgHumidity, 1, 2, humString);
  dtostrf(fillLevel, 1, 2, cisternFillString);

  if (sendMQTTMessages)
  {
    mqttSuccess = mqttClientLib->publish((baseTopic + "/temperatur/M1/" + sensorName).c_str(), String(tempString), true, 2);
    mqttSuccess ? blinkLed(GREEN) : blinkLed(RED, true);
    mqttClientLib->publish((baseTopic + "/luftfeuchtigkeit/M1/" + sensorName).c_str(), String(humString), true, 2);
    // Same string the restore subscribes to, so the two can never drift apart.
    mqttClientLib->publish(mqtt_FillLevelTopic.c_str(), String(cisternFillString), true, 2);
  }
  Serial.println("Temperature: " + String(avgTemperature) + "°C, Humidity: " + String(avgHumidity) + "%, Cistern Fill Level: " + String(fillLevel) + "% (shortest of " + String(validDistanceCount) + " readings, " + String(timeouts) + " timeouts), Version: " + version);
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
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqtt_port, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT(true);

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

    if (fillLevelSubscribePending) {
      fillLevelSubscribePending = false;
      mqttClientLib->subscribe(mqtt_FillLevelTopic);
    }

    if (fillLevelUnsubscribePending) {
      fillLevelUnsubscribePending = false;
      mqttClientLib->unsubscribe(mqtt_FillLevelTopic);
    }

    if (millis() - lastHeartbeatMs >= HEARTBEAT_INTERVAL_MS) {
      lastHeartbeatMs = millis();
      mqttClientLib->publishStatus("M1", "KellerDevice", sensorName, String(version));
    }
  }
  delay(100);
}

