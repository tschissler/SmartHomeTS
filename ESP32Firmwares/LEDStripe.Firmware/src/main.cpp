#include <Arduino.h>
#include <ArduinoJson.h>
#include <FastLED.h>
#include <WiFi.h>
#include <time.h>
#include "main.h"

// Shared libraries (same structure as TemperatureSensor2)
#include "ESP32Helpers.h"
#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"

const char* version = FIRMWARE_VERSION;

#define DATA_PIN_1 13
#define DATA_PIN_2 12
#define NUM_LEDS 256
CRGB ledsRight[NUM_LEDS];
CRGB ledsLeft[NUM_LEDS];

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern:
// WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);
WiFiClient wifiClient;

static MQTTClientLib *mqttClientLib = nullptr;
static String chipID = "";

static bool otaActive = false;
static unsigned long otaStartMs = 0;
static bool otaEnable = String(OTA_ENABLED) != "false";

// MQTT settings (kept compatible with existing backend topics)
static const String mqtt_broker = "smarthomepi2";
static const String mqtt_update_topic = "OTAUpdate/LEDStripe";
static const String mqtt_data_topic = "commands/illumination/LEDStripe/setColor";

static void mqttCallback(String &topic, String &payload);
static void connectToMQTT(bool cleanSession);

void setupTime() {
  // Set timezone (e.g., UTC +1:00)
  // Change according to your timezone
  // For UTC -5:00, use -5 * 3600, and so on
  configTime(1 * 3600, 0, "pool.ntp.org", "time.nist.gov");

  // Wait until time is set 
  const unsigned long startMs = millis();
  time_t now = time(nullptr);
  while (now < 8 * 3600 * 2) {
    if (millis() - startMs > 15000) {
      Serial.println("Timed out waiting for NTP time; continuing without valid time");
      break;
    }
    delay(250);
    now = time(nullptr);
  }

  struct tm timeinfo;
  if (!getLocalTime(&timeinfo)) {
    Serial.println("Failed to obtain time");
    return;
  }
  Serial.println(&timeinfo, "%A, %B %d %Y %H:%M:%S");
}

void clear(Panel panel) {
  for (int i = 0; i < NUM_LEDS; i++) {
    if (panel == Panel::BOTH || panel == Panel::RIGHT) {
      ledsRight[i] = CRGB(0, 0, 0);
    }
    if (panel == Panel::BOTH || panel == Panel::LEFT) {
      ledsLeft[i] = CRGB(0, 0, 0);
    }
  }
  FastLED.show();
}

void setColorFromJson(String jsonPayload) {
  // Parse JSON
  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, jsonPayload);
  if (error) {
    Serial.print("deserializeJson() failed: ");
    Serial.println(error.c_str());
    return;
  }

  // Extract RGBD values for left and right panels
  int rLeft = doc["Left"]["Red"];
  int gLeft = doc["Left"]["Green"];
  int bLeft = doc["Left"]["Blue"];
  int dLeft = doc["Left"]["Density"];
  bool onLeft = doc["Left"]["On"];

  int rRight = doc["Right"]["Red"];
  int gRight = doc["Right"]["Green"];
  int bRight = doc["Right"]["Blue"];
  int dRight = doc["Right"]["Density"];
  bool onRight = doc["Right"]["On"];

  if (onLeft) {
    setLEDColor(rLeft, gLeft, bLeft, dLeft, LEFT);
  } else {
    clear(LEFT);
  }
  if (onRight) {
    setLEDColor(rRight, gRight, bRight, dRight, RIGHT);
  } else {
    clear(RIGHT);
  }
}

void setLEDColor(int r, int g, int b, int d, Panel panel) {
  int trigger = (d == 0 ? 999 : 100 / d);
  int trigger2 = (d == 100 ? 999 : 100 / (100 - d));

  // Helper function to set LED color
  auto setLed = [&](int i, bool condition) {
    if (panel == Panel::BOTH || panel == Panel::RIGHT) {
      ledsRight[i] = condition ? CRGB(r, g, b) : CRGB(0, 0, 0);
    } 
    if (panel == Panel::BOTH || panel == Panel::LEFT) {
      ledsLeft[i] = condition ? CRGB(r, g, b) : CRGB(0, 0, 0);
    }
  };

  // Set LED color
  for (int i = 0; i < NUM_LEDS; i++) {
    bool condition = (d <= 50) ? (i % trigger == 0) : (i % trigger2 != 0);
    setLed(i, condition);
  }

  FastLED.show();
}

void initLEDGreen(Panel panel) {
  for (int i = 0; i < NUM_LEDS; i++) {
    if (panel == Panel::BOTH || panel == Panel::RIGHT) {
      ledsRight[i] = CRGB(10, 80, 10);
    }
    if (panel == Panel::BOTH || panel == Panel::LEFT) {
      ledsLeft[i] = CRGB(10, 80, 10);
    }
  }
  FastLED.show();
}

static void mqttCallback(String &topic, String &payload)
{
  Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

  if (topic == mqtt_update_topic)
  {
    if (otaActive || !otaEnable)
    {
      if (otaActive)
        Serial.println("OTA in progress, ignoring message");
      if (!otaEnable)
        Serial.println("OTA disabled, ignoring message");
      return;
    }

    String updateVersion = AzureOTAUpdater::ExtractVersionFromUrl(payload);
    Serial.println("Current firmware version is " + String(version));
    Serial.println("New firmware version is " + updateVersion);

    if (updateVersion.length() > 0 && String(version) == updateVersion)
    {
      Serial.println("Firmware is up to date");
      return;
    }

    Serial.println("OTA Update Triggered");
    otaActive = true;
    otaStartMs = millis();
    AzureOTAUpdater::UpdateFirmwareFromUrl(payload.c_str());
    return;
  }

  if (topic == mqtt_data_topic)
  {
    Serial.println("Setting LED color from MQTT message");
    setColorFromJson(payload);
    return;
  }

  Serial.println("Unknown topic, ignoring message");
}

static void connectToMQTT(bool cleanSession)
{
  Serial.print("WiFi Status: ");
  Serial.println(WiFi.status() == WL_CONNECTED ? "Connected" : "Disconnected");

  if (WiFi.status() != WL_CONNECTED)
  {
    Serial.println("WiFi not connected, attempting to reconnect...");
    wifiLib.connect();
  }

  mqttClientLib->connect(cleanSession);
  mqttClientLib->subscribe({mqtt_update_topic, mqtt_data_topic});
  Serial.println("### MQTT Client is connected and subscribed to topics");
  Serial.println("OTA Topic: " + mqtt_update_topic);
  Serial.println("Data Topic: " + mqtt_data_topic);
}

void setup() {
  FastLED.addLeds<WS2812B, DATA_PIN_1, GRB>(ledsRight, NUM_LEDS);
  FastLED.addLeds<WS2812B, DATA_PIN_2, GRB>(ledsLeft, NUM_LEDS);
  Serial.begin(115200);
  Serial.print("LEDStripe ");
  Serial.println(version);

  chipID = ESP32Helpers::getChipId();
  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);

  // Connect to WiFi (same structure as TemperatureSensor2)
  Serial.print("Connecting to WiFi ");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();

  setupTime();

  initLEDGreen(LEFT);
  delay(500);
  clear(LEFT);
  initLEDGreen(RIGHT);
  delay(500);
  clear(RIGHT);
  initLEDGreen(LEFT);
  delay(500);
  clear(LEFT);
  initLEDGreen(RIGHT);
  delay(500);
  clear(RIGHT);
  
  // Set up MQTT
  String mqttClientID = "ESP32LEDStripeClient_" + chipID;
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT(true);

  mqttClientLib->publish("meta/LEDStripe/version", String(version), true, 2);
}

void loop() {
  if (otaActive)
  {
    const int otaStatus = AzureOTAUpdater::CheckUpdateStatus();
    if (otaStatus < 0)
    {
      Serial.println("OTA failed; resuming normal operation. Next OTA will trigger on new MQTT message.");
      otaActive = false;
    }
    else if (otaStatus == 0)
    {
      // If OTA didn't transition to UPDATING within a reasonable time, give up and stay functional.
      if (millis() - otaStartMs > 120000)
      {
        Serial.println("OTA did not succeed within timeout; resuming normal operation.");
        otaActive = false;
      }
    }

    delay(200);
    return;
  }

  {
    bool mqttConnected = mqttClientLib->loop();
    if (!mqttConnected)
    {
      int lastErr = mqttClientLib->lastError();
      Serial.print("MQTT loop() returned false! Last Error Code: ");
      Serial.println(lastErr);
      Serial.print("WiFi Status: ");
      Serial.println(WiFi.status() == WL_CONNECTED ? "Connected" : "Disconnected");
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT(false);
    }
  }

  delay(200);
}
