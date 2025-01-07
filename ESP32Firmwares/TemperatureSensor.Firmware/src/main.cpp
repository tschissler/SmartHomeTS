#include <Arduino.h>
#include <WiFi.h>
#include "DHT.h"
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>

#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "TFTDisplay.h"

#include "soc/soc.h"
#include "soc/rtc_cntl_reg.h"

// Pin configuration
#define LED_INTERNAL_PIN 2
#define DHTPIN 25    
#define SWITCH_TOP_PIN 33
#define SWITCH_BOTTOM_PIN 32

// Initialize TFT Display
TFTDisplay tftDisplay;

const char* version = TEMPSENSORFW_VERSION;
String chipID = "";

#define BLINK_DURATION 10       // Blink duration in milliseconds, blinking will happen every second
 
#define DHTTYPE DHT22   
DHT dht(DHTPIN, DHTTYPE);

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
String ssid;
String passwords = WIFI_PASSWORDS;
String password;

WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP);
MQTTClientLib* mqttClientLib = nullptr;

static bool otaInProgress = false;
static bool otaEnable = true;
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;
static int lastMQTTSentMinute = 0;
static int switchTopStatus = false;
static int switchBottomStatus = false;

static String baseTopic = "";
static String sensorName = "";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/TemperaturSensor";
static String mqtt_ConfigTopic = "config/TemperaturSensor/Sensorname/";

String extractVersionFromUrl(String url) {
    int lastUnderscoreIndex = url.lastIndexOf('_');
    int lastDotIndex = url.lastIndexOf('.');

    if (lastUnderscoreIndex != -1 && lastDotIndex != -1 && lastDotIndex > lastUnderscoreIndex) {
        return url.substring(lastUnderscoreIndex + 1, lastDotIndex);
    }

    return ""; // Return empty string if the pattern is not found
}

void printInformationOnTFT(String temperature, String humidity, bool displayMQTTMessage) {
  
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
  data.ssid = ssid;
  data.rssi = String(WiFi.RSSI());
  data.displayMQTTMessage = displayMQTTMessage;
  data.mqttEnabled = sendMQTTMessages;
  data.mqttSuccess = mqttSuccess;
  data.pingSuccess = Ping.ping(mqtt_broker.c_str());

  tftDisplay.printInformation(data);
}

void mqttCallback(String &topic, String &payload) {
    Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

    if (topic == mqtt_ConfigTopic) {
      sensorName = payload;
      baseTopic = "data/" + sensorName + "/";
      Serial.println("Sensor name set to: " + sensorName);
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
    WiFi.begin(ssid, password);
    while (WiFi.status() != WL_CONNECTED) {
      delay(1000);
      Serial.print("Reconnecting to WiFi ");
      Serial.print(ssid);
      Serial.println(" ...");
    }
    Serial.println("Reconnected to WiFi");
  }
  mqttClientLib->connect({mqtt_ConfigTopic, mqtt_OTAtopic});
  Serial.println("Wifi is connected");
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
    mqttSuccess = mqttClientLib->publish((baseTopic + "temperatur").c_str(), String(tempString), true, 2);
    mqttClientLib->publish((baseTopic + "luftfeuchtigkeit").c_str(), String(humString), true, 2);
    mqttClientLib->publish(("meta/" + sensorName + "/version").c_str(), String(version), true, 2);
    
    if (digitalRead(SWITCH_TOP_PIN) != switchTopStatus) {
      switchTopStatus = digitalRead(SWITCH_TOP_PIN);
      mqttClientLib->publish((baseTopic + "fenster_oben").c_str(), switchTopStatus?"offen":"geschlossen", true, 2);
      Serial.println("Switch Top Status changed to " + String(switchTopStatus));
    }

    if (digitalRead(SWITCH_BOTTOM_PIN) != switchBottomStatus) {
      switchBottomStatus = digitalRead(SWITCH_BOTTOM_PIN);
      mqttClientLib->publish((baseTopic + "fenster_unten").c_str(), switchTopStatus?"offen":"geschlossen", true, 2);
      Serial.println("Switch Bottom Status changed to " + String(switchBottomStatus));
    }
  }
  Serial.println("Temperature: " + String(temperature) + "°C, Humidity: " + String(humidity) + "%, Version: " + version);
  printInformationOnTFT(String(temperature), String(humidity), true);
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
  Serial.print("Configured WiFi networks: ");
  Serial.println(passwords.length());
  for (int i = 0; i < numberOfNetworks; i++) {
    if (WiFi.RSSI(i) > maxRSSI && passwords.indexOf(WiFi.SSID(i)) >=0) {
      maxRSSI = WiFi.RSSI(i);
      maxRSSIIndex = i;
    }
  }
  if (maxRSSIIndex == -1) {
    Serial.println("No WiFi network found");
    return;
  } else {
    Serial.println("Strongest known WiFi network is " + WiFi.SSID(maxRSSIIndex) + " with RSSI " + WiFi.RSSI(maxRSSIIndex) + " dBm");
    ssid = WiFi.SSID(maxRSSIIndex);
    password = passwords.substring(passwords.indexOf(ssid) + ssid.length() + 1, passwords.indexOf('|', passwords.indexOf(ssid)));
    return;
  }
}

void setup() {
  WRITE_PERI_REG(RTC_CNTL_BROWN_OUT_REG, 0); //disable brownout detector

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

  //baseTopic = "data/" + chipID + "/"; // Base topic with the chipID
  mqtt_ConfigTopic += chipID;

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
  
  // Initialize display
  tftDisplay.init();

  // Set up MQTT
  String mqttClientID = "ESP32TemperatureSensorClient_" + chipID;
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
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

  printInformationOnTFT("-", "-", false);
}

void loop() {
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();

  if (!otaInProgress) {
    // Transmit data every minute
    int currentMinute = timeClient.getMinutes();
    if(currentMinute != lastMQTTSentMinute) {
      lastMQTTSentMinute = currentMinute;

      readSensorAndPublish();
    } 

    if(!mqttClientLib->loop())
    {
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT();
    }
    bool pingSuccess = Ping.ping(mqtt_broker.c_str());

    digitalWrite(LED_INTERNAL_PIN, HIGH);
    delay(BLINK_DURATION);
    digitalWrite(LED_INTERNAL_PIN, LOW);
    if (!pingSuccess) {
      Serial.println("Ping failed");
      delay(BLINK_DURATION);
      digitalWrite(LED_INTERNAL_PIN, HIGH);
      delay(BLINK_DURATION);
      digitalWrite(LED_INTERNAL_PIN, LOW);
    }
    if (!mqttSuccess) {
      Serial.println("MQTT failed");
      delay(BLINK_DURATION);
      digitalWrite(LED_INTERNAL_PIN, HIGH);
      delay(BLINK_DURATION);
      digitalWrite(LED_INTERNAL_PIN, LOW);
      delay(BLINK_DURATION);
      digitalWrite(LED_INTERNAL_PIN, HIGH);
      delay(BLINK_DURATION);
      digitalWrite(LED_INTERNAL_PIN, LOW);
    }
  }
  delay(100);
}
