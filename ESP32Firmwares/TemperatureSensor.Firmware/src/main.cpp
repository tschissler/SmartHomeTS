#include <Arduino.h>
#include <ArduinoJson.h>
#include <WiFi.h>
#include "DHT.h"
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>

#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "TFTDisplay.h"
#include "WifiLib.h"

#include "soc/soc.h"
#include "soc/rtc_cntl_reg.h"

// Pin configuration
// CAUTION: GPIO 2 carries the on-board LED *and* the TFT's D/C line (TFT_DC in
// TFTDisplay.h). On a device with a display the LED must never be driven, or the
// status blinking would corrupt the SPI traffic - see setStatusLed().
#define LED_INTERNAL_PIN 2
#define DHTPIN 25    
#define SWITCH_TOP_PIN 33
#define SWITCH_BOTTOM_PIN 32

// Initialize TFT Display
TFTDisplay tftDisplay;

const char* version = TEMPSENSORFW_VERSION;
String chipID = "";

// The LED stays dark while everything works; a blink pattern means something is wrong.
// Driven from millis() instead of the loop cadence, so the pattern stays readable no
// matter how long a loop iteration takes.
#define BLINK_SLOT_MS 200       // Length of one on- or off-slot of the blink pattern
#define BLINK_CYCLE_MS 2000     // Pattern repeats every 2 seconds
 
#define DHTTYPE DHT22   
DHT dht(DHTPIN, DHTTYPE);

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);

WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP);
MQTTClientLib* mqttClientLib = nullptr;

static bool otaInProgress = false;
static bool otaEnable = true;
static bool sendMQTTMessages = true;
// Only the dev board has a TFT attached; the sensors in the field do not. Configured
// via MQTT and defaulting to false, so a device without config leaves GPIO 2 alone.
static bool hasDisplay = false;
static bool displayInitialized = false;
static bool mqttSuccess = false;
static bool lastPingSuccess = false;
static int lastMQTTSentMinute = 0;
static int switchTopStatus = false;
static int switchBottomStatus = false;

static String baseTopic = "daten";
static String sensorName = "";
static String location = "unknown";
const String mqtt_broker = "mosquitto.intern";
const int mqtt_port = 1883;
static String mqtt_OTAtopic = "OTAUpdate/TemperaturSensor";
// Retained device heartbeat on status/<location>/<deviceType>/<deviceName>, so a consumer
// can tell a silent device from a healthy one. Must match the OTA topic segment.
static const uint32_t HEARTBEAT_INTERVAL_MS = 60000;
static uint32_t lastHeartbeatMs = 0;
static String mqtt_ConfigTopic = "config/TemperaturSensor/";

String extractVersionFromUrl(String url) {
    int lastUnderscoreIndex = url.lastIndexOf('_');
    int lastDotIndex = url.lastIndexOf('.');

    if (lastUnderscoreIndex != -1 && lastDotIndex != -1 && lastDotIndex > lastUnderscoreIndex) {
        return url.substring(lastUnderscoreIndex + 1, lastDotIndex);
    }

    return ""; // Return empty string if the pattern is not found
}

void printInformationOnTFT(String temperature, String humidity, bool displayMQTTMessage) {
  if (!hasDisplay) {
    return;
  }

  // Initialized on first use rather than in setup(), because the display is only known
  // to exist once the MQTT config has arrived.
  if (!displayInitialized) {
    tftDisplay.init();
    displayInitialized = true;
  }

  DisplayInformation data;
  
  // Update NTP client
  timeClient.update();

  // Get current time
  data.time = timeClient.getFormattedTime();
  data.temperature = temperature;
  data.humidity = humidity;
  data.version = version;
  data.chipID = chipID;
  data.sensorName = sensorName;
  data.ip = WiFi.localIP().toString();
  data.ssid = wifiLib.getSSID();
  data.rssi = String(WiFi.RSSI());
  data.displayMQTTMessage = displayMQTTMessage;
  data.mqttEnabled = sendMQTTMessages;
  data.mqttSuccess = mqttSuccess;
  data.pingSuccess = lastPingSuccess;

  tftDisplay.printInformation(data);
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

  if (!doc["HasDisplay"].isNull()) {
    hasDisplay = doc["HasDisplay"].as<bool>();
    Serial.println("Display support set to: " + String(hasDisplay ? "true" : "false"));
    if (hasDisplay) {
      // Bring the display up right away instead of waiting for the next sensor reading.
      printInformationOnTFT("-", "-", false);
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
  mqttClientLib->connect(false);
  mqttClientLib->subscribe(mqtt_ConfigTopic);
  mqttClientLib->subscribe(mqtt_OTAtopic);
  Serial.println("MQTT Client is connected");
}

void readSensorAndPublish() {
  if (sensorName == "") {
    Serial.println("Sensor name not set, skipping sensor reading");
    return;
  }

  float humidity = dht.readHumidity();
  float temperature = dht.readTemperature();

  if (isnan(humidity) || isnan(temperature)) {
    Serial.println("Failed to read from DHT sensor!");
    return;
  }

  char tempString[8];
  char humString[8];
  dtostrf(temperature, 1, 2, tempString);
  dtostrf(humidity, 1, 2, humString);

  if (sendMQTTMessages)
  {
    mqttSuccess = mqttClientLib->publish((baseTopic + "/temperatur/" + location + "/" + sensorName).c_str(), String(tempString), true, 2);
    mqttClientLib->publish((baseTopic + "/luftfeuchtigkeit/" + location + "/" + sensorName).c_str(), String(humString), true, 2);
    
    // if (digitalRead(SWITCH_TOP_PIN) != switchTopStatus) {
    //   switchTopStatus = digitalRead(SWITCH_TOP_PIN);
    //   mqttClientLib->publish((baseTopic + "/fenster/fenster_oben/" + sensorName).c_str(), switchTopStatus?"offen":"geschlossen", true, 2);
    //   Serial.println("Switch Top Status changed to " + String(switchTopStatus));
    // }

    // if (digitalRead(SWITCH_BOTTOM_PIN) != switchBottomStatus) {
    //   switchBottomStatus = digitalRead(SWITCH_BOTTOM_PIN);
    //   mqttClientLib->publish((baseTopic + "/fenster/fenster_unten/" + sensorName).c_str(), switchTopStatus?"offen":"geschlossen", true, 2);
    //   Serial.println("Switch Bottom Status changed to " + String(switchBottomStatus));
    // }
  }
  Serial.println("Temperature: " + String(temperature) + "°C, Humidity: " + String(humidity) + "%, Version: " + version);
  printInformationOnTFT(String(temperature), String(humidity), true);
}

// Both helpers are no-ops on a device with a display, where GPIO 2 belongs to the TFT.
void setStatusLed(bool on) {
  if (hasDisplay) {
    return;
  }
  digitalWrite(LED_INTERNAL_PIN, on ? HIGH : LOW);
}

// Dark while healthy; 2 flashes when the broker is unreachable, 3 when publishing failed
// (which also covers a device that has not received its config yet and sends nothing).
void updateStatusLed() {
  int blinks = 0;
  if (!lastPingSuccess) {
    blinks = 2;
  }
  if (!mqttSuccess) {
    blinks = 3;
  }

  if (blinks == 0) {
    setStatusLed(false);
    return;
  }

  uint32_t slot = (millis() % BLINK_CYCLE_MS) / BLINK_SLOT_MS;
  setStatusLed(slot < (uint32_t)(blinks * 2) && slot % 2 == 0);
}

void setup() {
  WRITE_PERI_REG(RTC_CNTL_BROWN_OUT_REG, 0); //disable brownout detector
  Serial.begin(9600);
  Serial.print("TemperatureSensor version ");
  Serial.println(version);
  chipID += String((uint16_t)(ESP.getEfuseMac() >> 32), HEX);
  chipID += String((uint32_t)ESP.getEfuseMac(), HEX);
  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);
  mqtt_ConfigTopic += chipID;


  // Connect to WiFi
  Serial.print("Connecting to WiFi ");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();
  String ssid = wifiLib.getSSID();

  // The display is not initialized here: whether one exists arrives with the MQTT config,
  // and on a device without one the init would claim GPIO 2 as the TFT's D/C line.
  pinMode(LED_INTERNAL_PIN, OUTPUT);
  setStatusLed(false);

  // Set up MQTT
  String mqttClientID = "ESP32TemperatureSensorClient_" + chipID;
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqtt_port, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT();

  //Init DHT sensor
  dht.begin();
  Serial.println("DHT sensor initialized");

  // Init switch pins
  pinMode(SWITCH_TOP_PIN, INPUT_PULLUP);
  pinMode(SWITCH_BOTTOM_PIN, INPUT_PULLUP);
  
  // Initialize NTPClient
  timeClient.begin();
  timeClient.setTimeOffset(0); // Set your time offset from UTC in seconds

  // Update NTP client
  timeClient.update();

  mqttClientLib->publish(("meta/" + sensorName + "/version/TemperaturSensor").c_str(), String(version), true, 2);

  printInformationOnTFT("-", "-", false);
}

void loop() {
  // CheckUpdateStatus() returns an int: 1 while updating, -1 on failure. Assigning it
  // straight to a bool made a failed update read as "still updating" forever, which
  // left the loop dead until the next power cycle.
  otaInProgress = (AzureOTAUpdater::CheckUpdateStatus() == 1);

  if (!otaInProgress) {
    // Transmit data every minute
    int currentMinute = timeClient.getMinutes();
    if(currentMinute != lastMQTTSentMinute) {
      lastMQTTSentMinute = currentMinute;

      readSensorAndPublish();
      // Keep the LED dark across the ping, which blocks for seconds and would otherwise
      // freeze it mid-pattern.
      setStatusLed(false);
      lastPingSuccess = Ping.ping(mqtt_broker.c_str());
    } 

    if(!mqttClientLib->loop())
    {
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT();
    }

    if (millis() - lastHeartbeatMs >= HEARTBEAT_INTERVAL_MS) {
      lastHeartbeatMs = millis();
      mqttClientLib->publishStatus(location, "TemperaturSensor", sensorName, String(version));
    }
    updateStatusLed();
  }
  delay(100);
}

