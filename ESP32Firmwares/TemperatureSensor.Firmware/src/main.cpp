#include <Arduino.h>
#include <WiFi.h>
#include <MQTT.h>
#include "DHT.h"
#include "AzureOTAUpdater.h"

const char* version = TEMPSENSORFW_VERSION;
String chipID = "";

// Deep Sleep Configuration
#define TIME_TO_SLEEP  60        // Time in seconds for ESP32 to sleep

#define DHTPIN 25     
#define DHTTYPE DHT22   
DHT dht(DHTPIN, DHTTYPE);

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
String ssid;
String passwords = WIFI_PASSWORDS;
String password;

// MQTT Broker settings
const char* mqtt_broker = "smarthomepi2";
const int mqtt_port = 32004;
const char* mqtt_OTAtopic = "OTAUpdateTemperatureSensor";

WiFiClient espClient;
MQTTClient mqttClient;

static bool otaInProgress = false;

String extractVersionFromUrl(String url) {
    int lastUnderscoreIndex = url.lastIndexOf('_');
    int lastDotIndex = url.lastIndexOf('.');

    if (lastUnderscoreIndex != -1 && lastDotIndex != -1 && lastDotIndex > lastUnderscoreIndex) {
        return url.substring(lastUnderscoreIndex + 1, lastDotIndex);
    }

    return ""; // Return empty string if the pattern is not found
}

void mqttCallback(String topic, String &payload) {
    if (otaInProgress)
      return;
    
    Serial.print("Message arrived on topic: ");
    Serial.print(topic);
    Serial.print(". Message: ");
    Serial.println(payload);

    if (topic == mqtt_OTAtopic) {
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
}

void connectToMQTT() {
    mqttClient.begin(mqtt_broker, mqtt_port, espClient);
    
    while (!mqttClient.connected()) {
        if (mqttClient.connect("ESP32TemperatureSensorClient")) {
            mqttClient.subscribe(mqtt_OTAtopic);
            Serial.println("Connected to MQTT Broker from connectToMQTT");
        } else {
            Serial.print("Failed to connect to MQTT Broker: ");
            Serial.println(mqtt_broker);
            delay(5000);
        }
    }
    mqttClient.onMessage(mqttCallback);
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
    mqttClient.begin(mqtt_broker, mqtt_port, espClient);
    mqttClient.onMessage(mqttCallback);
    while (!mqttClient.connected()) {
        if (mqttClient.connect("ESP32TemperatureSensorClient")) {
            mqttClient.subscribe(mqtt_OTAtopic);
            Serial.println("Connected to MQTT Broker from reconnect");
        } else {
            Serial.print("Failed to connect to MQTT Broker: ");
            Serial.println(mqtt_broker);
            delay(5000);
        }
    }
}

void readSensorAndPublish() {
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
  
  mqttClient.publish((baseTopic + "temperature").c_str(), String(tempString));
  mqttClient.publish((baseTopic + "humidity").c_str(), String(humString));
  mqttClient.publish(("meta/" + chipID + "/version").c_str(), String(version));
  Serial.println("Published new values to MQTT Broker");
  Serial.println("Temperature: " + String(temperature) + "°C, Humidity: " + String(humidity) + "%, Version: " + version);
}

void findWifi() {
  Serial.println("Scanning for WiFi networks...");
  int numberOfNetworks = WiFi.scanNetworks();
  Serial.print("Found ");
  Serial.print(numberOfNetworks);
  Serial.println(" networks.");
  for (int i = 0; i < numberOfNetworks; i++) {
    Serial.print(WiFi.SSID(i));
    Serial.print(" (");
    Serial.print(WiFi.RSSI(i));
    Serial.print(") ");
    Serial.println(WiFi.encryptionType(i));
    delay(10);
  }

  // Identify the strongest WiFi signal
  int maxRSSI = -1000;
  int maxRSSIIndex = -1;
  for (int i = 0; i < numberOfNetworks; i++) {
    if (WiFi.RSSI(i) > maxRSSI) {
      maxRSSI = WiFi.RSSI(i);
      maxRSSIIndex = i;
    }
  }
  if (maxRSSIIndex == -1) {
    Serial.println("No WiFi network found");
    return;
  } else {
    Serial.println("Strongest WiFi network is " + WiFi.SSID(maxRSSIIndex) + " with RSSI " + WiFi.RSSI(maxRSSIIndex) + " dBm");
    ssid = WiFi.SSID(maxRSSIIndex);
    password = passwords.substring(passwords.indexOf(ssid) + ssid.length() + 1, passwords.indexOf('|', passwords.indexOf(ssid)));
    return;
  }
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

  findWifi();
  while (ssid == "" || password == "") {
    Serial.println("No WiFi network found, retrying...");
    delay(1000);
    findWifi();
  }

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
  connectToMQTT();

  //Init DHT sensor
  dht.begin();
  Serial.println("DHT sensor initialized");
}

void loop() {
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();
  if (!mqttClient.connected()) {
    Serial.println("MQTT Client not connected, reconnecting...");
    reconnect();
  }
  readSensorAndPublish();
  mqttClient.loop();

  delay(TIME_TO_SLEEP * 1000);
}
