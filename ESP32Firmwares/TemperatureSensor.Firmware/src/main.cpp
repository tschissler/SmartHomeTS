#include <Arduino.h>
#include <WiFi.h>
#include <PubSubClient.h>
#include <ESP32httpUpdate.h>
#include "DHT.h"

// Deep Sleep Configuration
#define TIME_TO_SLEEP  30        // Time in seconds for ESP32 to sleep


#define DHTPIN 12     
#define DHTTYPE DHT11   
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


void updateFirmwareFromUrl(const String &firmwareUrl) {
    Serial.print("Downloading new firmware from: ");
    Serial.println(firmwareUrl);
    
    t_httpUpdate_return ret = ESPhttpUpdate.update(firmwareUrl);
    
    switch(ret) {
        case HTTP_UPDATE_FAILED:
            Serial.printf("HTTP_UPDATE_FAILD Error (%d): %s", ESPhttpUpdate.getLastError(), ESPhttpUpdate.getLastErrorString().c_str());
            break;

        case HTTP_UPDATE_NO_UPDATES:
            Serial.println("HTTP_UPDATE_NO_UPDATES");
            break;

        case HTTP_UPDATE_OK:
            Serial.println("HTTP_UPDATE_OK");
                Serial.println("Update done");
            break;
    }
    Serial.println();
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
    Serial.print("Message arrived on topic: ");
    Serial.print(topic);
    Serial.print(". Message: ");

    String messageTemp;

    for (int i = 0; i < length; i++) {
        messageTemp += (char)message[i];
    }

    Serial.println(messageTemp);

    if (String(topic) == mqtt_OTAtopic) {
          // Trigger OTA Update
          Serial.println("OTA Update Triggered");
          String firmwareUrl = messageTemp;
          updateFirmwareFromUrl(firmwareUrl);
    }
}

void reconnect() {
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
  mqttClient.loop();

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

  mqttClient.publish("M3/esp32/temperature", tempString);
  mqttClient.publish("M3/esp32/humidity", humString);
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
  Serial.println("TemperatiureSensor v0.1.2");

  // Connect to WiFi
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(1000);
    Serial.println("Connecting to WiFi...");
  }
  Serial.println("Connected to WiFi");
  
  // Print the IP address
  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  // Set up MQTT
  mqttClient.setCallback(mqttCallback);
  connectToMQTT();

  //Init DHT sensor
  dht.begin();
  Serial.println("DHT sensor initialized");

  readSensorAndPublish();

  goToDeepSleep();
}

void loop() {
  if (!mqttClient.connected()) {
    connectToMQTT();
  }
  mqttClient.loop();
}
