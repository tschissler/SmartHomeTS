#include <Arduino.h>
#include <WiFi.h>
#include <MQTT.h>
#include "DHT.h"
#include "AzureOTAUpdater.h"
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>

#include <Adafruit_GFX.h>    // Core graphics library
#include <Adafruit_ST7735.h> // Hardware-specific library for ST7735

#include "soc/soc.h"
#include "soc/rtc_cntl_reg.h"

// Pin configuration
#define TFT_CS     15
#define TFT_RST    4  
#define TFT_DC     2
#define TFT_MOSI   23
#define TFT_SCLK   18
#define LED_INTERNAL_PIN 2
#define DHTPIN 25    
#define SWITCH_TOP_PIN 33
#define SWITCH_BOTTOM_PIN 32

// Initialize Adafruit ST7735
Adafruit_ST7735 tft = Adafruit_ST7735(TFT_CS, TFT_DC, TFT_MOSI, TFT_SCLK, TFT_RST);

const char* version = TEMPSENSORFW_VERSION;
String chipID = "";

// Deep Sleep Configuration
#define BLINK_DURATION 100       // Blink duration in milliseconds, blinking will happen every second
 
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
const char* mqtt_OTAtopic = "OTAUpdate/TemperatureSensor";
// Define the maximum packet size for the MQTT client
#define MQTT_MAX_PACKET_SIZE 4096

WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP);
MQTTClient mqttClient(MQTT_MAX_PACKET_SIZE);

static bool otaInProgress = false;
static bool otaEnable = true;
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;
static int lastMQTTSentMinute = 0;
static int switchTopStatus = false;
static int switchBottomStatus = false;

static String baseTopic = "";

String extractVersionFromUrl(String url) {
    int lastUnderscoreIndex = url.lastIndexOf('_');
    int lastDotIndex = url.lastIndexOf('.');

    if (lastUnderscoreIndex != -1 && lastDotIndex != -1 && lastDotIndex > lastUnderscoreIndex) {
        return url.substring(lastUnderscoreIndex + 1, lastDotIndex);
    }

    return ""; // Return empty string if the pattern is not found
}

void printInformationOnTFT(String temperature, String humidity, bool mqttMessage) {
  // Clear TFT screen
  tft.fillScreen(ST77XX_BLACK);
  tft.setCursor(0, 0);

  tft.println("TemperatureSensor");
  tft.print("Version: ");
  tft.println(version);
  tft.print("Chip ID: ");
  tft.println(chipID);

  tft.setCursor(0, 30);

  // Update NTP client
  timeClient.update();

  // Get current time
  String formattedTime = timeClient.getFormattedTime();
  tft.println(formattedTime);
  tft.println("Temperature: " + temperature + "C");
  tft.println("Humidity: " + humidity + "%");
  tft.println("IP: " + WiFi.localIP().toString());
  tft.println("SSID: " + ssid);
  tft.println("RSSI: " + String(WiFi.RSSI()) + " dBm");

  if (mqttMessage) {
    tft.println();
    if (sendMQTTMessages) {
      tft.println(mqttSuccess?"MQTT message sent\nsuccessfully":"Sending MQTT\nmessage failed");
    } else {
      tft.println("MQTT messages disabled");
    }
  }

  tft.println(Ping.ping(mqtt_broker)?"Ping successful":"Ping failed");
}
void mqttCallback(String topic, String &payload) {
    Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);
    if (otaInProgress || !otaEnable) {
      if (otaInProgress)
        Serial.println("OTA in progress, ignoring message");
      if (!otaEnable)
        Serial.println("OTA disabled, ignoring message");
      return;
    }

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
  Serial.println("Wifi is connected");
  mqttClient.begin(mqtt_broker, mqtt_port, wifiClient);
  mqttClient.onMessage(mqttCallback);
  mqttClient.setOptions(60, false, 1000);
  while (!mqttClient.connected()) {
    String clientId = "ESP32TemperatureSensorClient_" + chipID;
    Serial.println("ClientId = " + clientId);
    if (mqttClient.connect(clientId.c_str())) {
      bool subscribeSuccess = mqttClient.subscribe(mqtt_OTAtopic);
      if (subscribeSuccess) {
        Serial.print("Subscribed to topic: ");
        Serial.println(mqtt_OTAtopic);
      } else {
        Serial.print("Failed to subscribe to topic: ");
        Serial.println(mqtt_OTAtopic);
        Serial.print("Last Error: ");
        Serial.println(mqttClient.lastError());
      }
      Serial.print("Connected to MQTT Broker ");
      Serial.print(mqtt_broker);
      Serial.print(" with Connection Status: ");
      Serial.println(mqttClient.connected());
    } else {
        Serial.print("Failed to connect to MQTT Broker: ");
        Serial.println(mqtt_broker);
        Serial.println(mqttClient.lastError());
        delay(1000);
    }
  }
}

void readSensorAndPublish() {
  // Stay awake for a short period to receive messages
  // unsigned long startMillis = millis();
  // while (millis() - startMillis < 10000) {
  //   mqttClient.loop();
  //   delay(10);  // Small delay to prevent WDT reset
  // }

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
    // if (!mqttClient.connected()) {
    //   Serial.println("MQTT Client not connected, reconnecting before publish...");
    //   connectToMQTT();
    // }
    
    mqttSuccess = mqttClient.publish("temp/temperature", "1.0", true, 2);
    mqttSuccess =  mqttClient.publish((baseTopic + "temperature").c_str(), String(tempString), true, 2);
    mqttClient.publish((baseTopic + "humidity").c_str(), String(humString), true, 2);
    mqttClient.publish(("meta/" + chipID + "/version").c_str(), String(version), true, 2);
    Serial.println(mqttSuccess?"Published new values to MQTT Broker":"Publishing to MQTT Broker failed");
    Serial.println(" -> Connected:" + String(mqttClient.connected()) + " -> LastError:"  + String(mqttClient.lastError())  + " -> ReturnCode:" + String(mqttClient.returnCode()));
  }
  Serial.println("Temperature: " + String(temperature) + "°C, Humidity: " + String(humidity) + "%, Version: " + version);
  printInformationOnTFT(String(temperature), String(humidity), true);
}

void sendMQTTMessage(String subtopic, String message)
{
  if (!mqttClient.connected()) {
      Serial.println("MQTT Client not connected, reconnecting before publish...");
      connectToMQTT();
    }
    
    mqttClient.publish(baseTopic + (subtopic).c_str(), String(message), true, 2);
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

  baseTopic = "data/" + chipID + "/"; // Base topic with the chipID

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
  tft.initR(INITR_BLACKTAB);      // Initialize a ST7735S chip, black tab
  tft.fillScreen(ST77XX_BLACK);   // Fill screen with black color
  tft.setRotation(1);             // Set orientation (0-3)

  // Text settings
  tft.setTextSize(1);             // Set text size
  tft.setTextColor(ST77XX_WHITE); // Set text color

  // // Set up MQTT
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

    if(!mqttClient.loop())
    {
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT();
    }
    // if (digitalRead(SWITCH_TOP_PIN) != switchTopStatus) {
    //   switchTopStatus = digitalRead(SWITCH_TOP_PIN);
    //   sendMQTTMessage("window_top", switchTopStatus?"open":"closed");
    //   Serial.println("Switch Top Status changed to " + String(switchTopStatus));
    // }

    // if (digitalRead(SWITCH_BOTTOM_PIN) != switchBottomStatus) {
    //   switchBottomStatus = digitalRead(SWITCH_BOTTOM_PIN);
    //   sendMQTTMessage("window_bottom", switchBottomStatus?"open":"closed");
    //   Serial.println("Switch Bottom Status changed to " + String(switchBottomStatus));
    // }

    // bool pingSuccess = Ping.ping(mqtt_broker);

    // digitalWrite(LED_INTERNAL_PIN, HIGH);
    // delay(BLINK_DURATION);
    // digitalWrite(LED_INTERNAL_PIN, LOW);
    // if (!pingSuccess) {
    //   Serial.println("Ping failed");
    //   delay(BLINK_DURATION);
    //   digitalWrite(LED_INTERNAL_PIN, HIGH);
    //   delay(BLINK_DURATION);
    //   digitalWrite(LED_INTERNAL_PIN, LOW);
    // }
    // if (!mqttSuccess) {
    //   Serial.println("MQTT failed");
    //   delay(BLINK_DURATION);
    //   digitalWrite(LED_INTERNAL_PIN, HIGH);
    //   delay(BLINK_DURATION);
    //   digitalWrite(LED_INTERNAL_PIN, LOW);
    //   delay(BLINK_DURATION);
    //   digitalWrite(LED_INTERNAL_PIN, HIGH);
    //   delay(BLINK_DURATION);
    //   digitalWrite(LED_INTERNAL_PIN, LOW);
    // }
  }
  delay(100);
}
