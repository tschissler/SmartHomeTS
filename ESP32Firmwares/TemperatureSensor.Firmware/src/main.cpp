#include <Arduino.h>
#include <WiFi.h>
#include <PubSubClient.h>
#include "DHT.h"
#include "AzureOTAUpdater.h"

const char* version = TEMPSENSORFW_VERSION;
String chipID = "";

// Deep Sleep Configuration
#define TIME_TO_SLEEP  3        // Time in seconds for ESP32 to sleep

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

static bool otaInProgress = false;

String extractVersionFromUrl(String url) {
    int lastUnderscoreIndex = url.lastIndexOf('_');
    int lastDotIndex = url.lastIndexOf('.');

    if (lastUnderscoreIndex != -1 && lastDotIndex != -1 && lastDotIndex > lastUnderscoreIndex) {
        return url.substring(lastUnderscoreIndex + 1, lastDotIndex);
    }

    return ""; // Return empty string if the pattern is not found
}

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
    String messageTemp;

    for (int i = 0; i < length; i++) {
        messageTemp += (char)message[i];
    }
    
    Serial.print("Message arrived on topic: ");
    Serial.print(topic);
    Serial.print(". Message: ");
    Serial.println(messageTemp);

    if (String(topic) == mqtt_OTAtopic) {
      String updateVersion = extractVersionFromUrl(messageTemp);
      Serial.println("Current firmware version is " + String(version));
      Serial.println("New firmware version is " + updateVersion);
      if( version != updateVersion.c_str()) {
          // Trigger OTA Update
          const char *firmwareUrl = messageTemp.c_str();
          
          bool result =  AzureOTAUpdater::UpdateFirmwareFromUrl(firmwareUrl);
          if (result) {
            Serial.println("OTA Update successful initiated, waiting to be finished");
          }
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
  Serial.println("Temperature: " + String(temperature) + "°C, Humidity: " + String(humidity) + "%, Version: " + version);
  delay(1000);
}

void setup() {
  Serial.begin(9600);
  Serial.print("TemperatureSensor version ");
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
  mqttClient.setCallback(mqttCallback);
  connectToMQTT();

  //Init DHT sensor
  dht.begin();
  Serial.println("DHT sensor initialized");
}

void loop() {
  if (!mqttClient.connected()) {
    reconnect();
  }
  readSensorAndPublish();
  mqttClient.loop();

  AzureOTAUpdater::CheckUpdateStatus();
  delay(TIME_TO_SLEEP * 1000);
}
