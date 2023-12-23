#include <Arduino.h>
#include <WiFi.h>
#include <PubSubClient.h>
#include "DHT.h"
#include <Preferences.h>
#include "AzureOTAUpdater.h"


const char* version = "0.1.99";
String chipID = "";

// Deep Sleep Configuration
#define TIME_TO_SLEEP  60        // Time in seconds for ESP32 to sleep

#define DHTPIN 25     
#define DHTTYPE DHT22   
DHT dht(DHTPIN, DHTTYPE);

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_SSID and WIFI_PASSWORD as environment variables on your dev-system
const char* ssid = WIFI_SSID;
const char* password = WIFI_PASSWORD;

// MQTT Broker settings
const char* mqtt_broker = "smarthomepi2";
const int mqtt_port = 32004;
const char* mqtt_OTAtopic = "OTAUpdateTemperatureSensor";

WiFiClient espClient;
PubSubClient mqttClient(espClient);
Preferences preferences;

static bool otaInProgress = false;


void connectToMQTT() {
    mqttClient.setServer(mqtt_broker, mqtt_port);
    while (!mqttClient.connected()) {
        if (mqttClient.connect("ESP32TemperatureSensorClient")) {
            mqttClient.subscribe(mqtt_OTAtopic);
            Serial.println("Connected to MQTT Broker");
        } else {
            Serial.print("Failed to connect to MQTT Broker: ");
            Serial.println(mqtt_broker);
            delay(5000);
        }
    }
}

void mqttCallback(char* topic, byte* message, unsigned int length) {
    return;
    Serial.print("Message arrived on topic: ");
    Serial.print(topic);
    Serial.print(". Message: ");

    String messageTemp;

    for (int i = 0; i < length; i++) {
        messageTemp += (char)message[i];
    }

    Serial.println(messageTemp);
    preferences.begin("config", false);

    if (String(topic) == mqtt_OTAtopic) {
      if( messageTemp != preferences.getString("firmwareUrl", "")) {
          // Trigger OTA Update
          Serial.print("Current firmware version: ");
          Serial.println(preferences.getString("firmwareUrl", ""));
          Serial.print("New firmware version: ");
          Serial.println(messageTemp);
          Serial.println("OTA Update Triggered");
          String firmwareUrl = messageTemp;
          // bool result = updateFirmwareFromUrl(firmwareUrl);
          // if (result) {
          //   Serial.println("OTA Update successful, recording new firmwareUrl in preferences");
          //   preferences.putString("firmwareUrl", firmwareUrl);
          // }
          preferences.end();
      }
      else {
        Serial.println("Firmware is up to date");
      }
    }
}

void reconnect() {
  if (WiFi.status() != WL_CONNECTED) {
    WiFi.begin(ssid, password);
    while (WiFi.status() != WL_CONNECTED) {
      delay(1000);
      Serial.print("Reconnecting to WiFi ");
      Serial.print(ssid);
      Serial.println(" ...");
    }
    Serial.println("Reconnected to WiFi");
  }
  while (!mqttClient.connected()) {
    if (mqttClient.connect("ESP32Client")) {
      connectToMQTT();
    } else {
      delay(5000);
    }
  }
}

void readSensorAndPublish() {
  if (!mqttClient.connected()) {
    reconnect();
  }
  // Stay awake for a short period to receive messages
  unsigned long startMillis = millis();
  while (millis() - startMillis < 10000) {
    mqttClient.loop();
    delay(10);  // Small delay to prevent WDT reset
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

  String baseTopic = "data/" + chipID + "/"; // Base topic with the chipID
  
  mqttClient.publish((baseTopic + "temperature").c_str(), tempString, true);
  mqttClient.publish((baseTopic + "humidity").c_str(), humString, true);
  mqttClient.publish(("meta/" + chipID + "/version").c_str(), version, true);
  Serial.println("Published new values to MQTT Broker");
  Serial.print("Temperature: ");
  Serial.print(temperature);
  Serial.print("°C, Humidity: ");
  Serial.print(humidity);
  Serial.print("%, Version: ");
  Serial.println(version);
  delay(1000);
}

void goToDeepSleep() {
  Serial.println("Going to sleep now");
  esp_sleep_enable_timer_wakeup(TIME_TO_SLEEP * 1000000);
  WiFi.disconnect(true);
  WiFi.mode(WIFI_OFF);
  esp_deep_sleep_start();
}

void setup() {
  Serial.begin(9600);
  Serial.print("TemperatureSensor ");
  Serial.println(version);

  // Get the high 2 bytes of the EFUSE MAC address, convert to hexadecimal, and append to the chipID String
  chipID += String((uint16_t)(ESP.getEfuseMac() >> 32), HEX);

  // Get the low 4 bytes of the EFUSE MAC address, convert to hexadecimal, and append to the chipID String
  chipID += String((uint32_t)ESP.getEfuseMac(), HEX);

  // Print the Chip ID
  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);


  // Connect to WiFi
  Serial.print("Connecting to WiFi ");
  Serial.println(ssid);
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(1000);
    Serial.println("Connecting to WiFi...");
  }
  Serial.println("Connected to WiFi");
  
  // Print the IP address
  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  // // Set up MQTT
  // mqttClient.setCallback(mqttCallback);
  // connectToMQTT();

  // //Init DHT sensor
  // dht.begin();
  // Serial.println("DHT sensor initialized");

  //readSensorAndPublish();

  //goToDeepSleep();
}

void loop() {
  // readSensorAndPublish();
  // mqttClient.loop();

  if (!otaInProgress)
  {
    const char* firmwareURL = "https://iotstoragem1.blob.core.windows.net/firmwareupdates/TemperatureSensorFirmware_7239430831.bin";
    AzureOTAUpdater::UpdateFirmwareFromUrl(firmwareURL);
    otaInProgress = true;
  }

  AzureOTAUpdater::CheckUpdateStatus();
  delay(1000);
}
