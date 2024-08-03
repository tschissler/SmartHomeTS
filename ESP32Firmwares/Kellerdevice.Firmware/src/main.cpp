#include <Arduino.h>
#include <WiFi.h>
#include <PubSubClient.h>
#include <time.h>
#include <ESP32httpUpdate.h>
#include <AzureRootCert.h>
#include <SPI.h>
#include "Adafruit_SHTC3.h"

const char* appName = "KellerDevice";
const char* version = "0.0.2";

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_SSID and WIFI_PASSWORD as environment variables on your dev-system
const char* ssid = WIFI_SSID;
const char* password = WIFI_PASSWORD;

// MQTT Broker settings
const char* mqtt_broker = "smarthomepi2";
const int mqtt_port = 32004;
const char* mqtt_update_topic = "OTAUpdateKellerdeviceTopic";

WiFiClient espClient;
PubSubClient mqttClient(espClient);

const int Red_LED_Pin = 13;
const int Green_LED_Pin = 12;
const int Blue_LED_Pin = 27;
const int I2CDataPin = 32;
const int I2CClockPin = 33;

Adafruit_SHTC3 shtc3 = Adafruit_SHTC3();

void setupTime() {
 // Set timezone to Central European Time (CET) with daylight saving time (CEST)
  // CET is UTC+1, CEST is UTC+2
  // The timezone string format is: "TZ=std offset dst offset, start[/time], end[/time]"
  // For CET/CEST: "CET-1CEST,M3.5.0/2,M10.5.0/3"
  // This means:
  // - Standard time is CET (UTC+1)
  // - Daylight saving time is CEST (UTC+2)
  // - DST starts on the last Sunday of March at 2:00 AM
  // - DST ends on the last Sunday of October at 3:00 AM
  configTzTime("CET-1CEST,M3.5.0/2,M10.5.0/3", "pool.ntp.org", "time.nist.gov");

  // Wait until time is set
  time_t now = time(nullptr);
  while (now < 8 * 3600 * 2) {
    delay(500);
    now = time(nullptr);
  }

  struct tm timeinfo;
  if (!getLocalTime(&timeinfo)) {
    Serial.println("Failed to obtain time");
    return;
  }
  Serial.println(&timeinfo, "%A, %B %d %Y %H:%M:%S");
}

void updateFirmwareFromUrl(const String &firmwareUrl) {
    Serial.print("Downloading new firmware from: ");
    Serial.println(firmwareUrl);

    digitalWrite(Red_LED_Pin, HIGH);
    digitalWrite(Blue_LED_Pin, HIGH);
    
    t_httpUpdate_return ret = ESPhttpUpdate.update(firmwareUrl);
    Serial.println("Firmware update process completed.");
    Serial.print("HTTP Update result: ");
    Serial.println(ret);
    
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

        default:
            Serial.println("Unknown response");
            Serial.println(ret);
            break;
    }
    Serial.println();

    digitalWrite(Red_LED_Pin, LOW);
    digitalWrite(Blue_LED_Pin, LOW);
}

void connectToMQTT() {
    mqttClient.setServer(mqtt_broker, mqtt_port);
    while (!mqttClient.connected()) {
        if (mqttClient.connect("ESP32KellerdeviceClient")) {
            mqttClient.subscribe(mqtt_update_topic);
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

    if (String(topic) == mqtt_update_topic) {
      // Trigger OTA Update
      Serial.println("OTA Update Triggered");
      String firmwareUrl = messageTemp;
      updateFirmwareFromUrl(firmwareUrl);
    }
}

void setup() {
  Serial.begin(9600);
  Serial.println(String(appName) + " " + String(version));  
  
  // Set LED pins as output
  pinMode(Red_LED_Pin, OUTPUT);
  pinMode(Green_LED_Pin, OUTPUT);
  pinMode(Blue_LED_Pin, OUTPUT);

  // Turn off all LEDs, turn on blue LED to indicate connecting to WiFi
  digitalWrite(Red_LED_Pin, LOW);
  digitalWrite(Green_LED_Pin, LOW);
  digitalWrite(Blue_LED_Pin, HIGH);

  // Connect to WiFi
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(1000);
    Serial.println("Connecting to WiFi " + String(ssid) + " with password " + String(password) + "...");
  }
  Serial.println("Connected to WiFi");

  setupTime();
  
  WiFiClientSecure client;
  client.setCACert(azure_root_cert);

  if (!client.connect("iotstoragem1.blob.core.windows.net", 443)) {
    Serial.println("Connection failed!");
  }
  
  // Print the IP address
  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  // Set up MQTT
  mqttClient.setCallback(mqttCallback);
  connectToMQTT();

  Serial.println("SHTC3 test");
  if (! shtc3.begin()) {
    Serial.println("Couldn't find SHTC3");
    while (1) delay(1);
  }
  Serial.println("Found SHTC3 sensor");
  
  digitalWrite(Blue_LED_Pin, LOW);
  digitalWrite(Green_LED_Pin, HIGH);

}

unsigned long previousMillisLED = 0;
unsigned long previousMillisSensor = 0;
const long intervalLED = 2000; // Interval for LED blinking (2 seconds)
const long intervalSensor = 10000; // Interval for sensor reading (10 seconds)

void loop() {
  if (!mqttClient.connected()) {
    connectToMQTT();
  }
  mqttClient.loop();

  sensors_event_t humidity, temp;

  unsigned long currentMillis = millis();

  // Check if it's time to blink the LED
  if (currentMillis - previousMillisLED >= intervalLED) {
    previousMillisLED = currentMillis;
    // Blink the LED
    digitalWrite(Green_LED_Pin, HIGH);
    delay(100);
    digitalWrite(Green_LED_Pin, LOW);
  }

  // Check if it's time to read the sensor
  if (currentMillis - previousMillisSensor >= intervalSensor) {
    previousMillisSensor = currentMillis;
    // Read the sensor data
    sensors_event_t humidity, temp;
    shtc3.getEvent(&humidity, &temp);
    mqttClient.publish("data/keller/temperature", String(temp.temperature).c_str());
    mqttClient.publish("data/keller/humidity", String(humidity.relative_humidity).c_str());
  }
}
