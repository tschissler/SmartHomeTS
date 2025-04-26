#include <Arduino.h>
#include <SoftwareSerial.h>
#include <deque>
#include <SMLParser.h>
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>
#include <memory>
#include <ArduinoJson.h>

#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"

const int irLedPin = 19; // Define the pin for the IR LED
const int irPhototransistorPin = 23;   // Define the pin for the IR sensor
const int ledPin = 18; // Define the pin for the LED

EspSoftwareSerial::UART serialPort;

#define BUFFER_SIZE 4096

const char* version = SMLSENSORFW_VERSION;
String chipID = "";

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);

WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP);
std::unique_ptr<MQTTClientLib> mqttClientLib = nullptr;

static bool otaInProgress = false;
static bool otaEnable = true;
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;

static String baseTopic = "daten";
static String sensorName = "";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/SMLSensor";
static String mqtt_ConfigTopic = "config/SMLSensor/Sensorname/";

std::deque<uint8_t> buffer;
const std::vector<uint8_t> startSequence = {0x1B, 0x1B, 0x1B, 0x1B, 0x01, 0x01, 0x01, 0x01};
const std::vector<uint8_t> endSequencePrefix = {0x1B, 0x1B, 0x1B, 0x1B, 0x1A};

String extractVersionFromUrl(String url) {
    int lastUnderscoreIndex = url.lastIndexOf('_');
    int lastDotIndex = url.lastIndexOf('.');

    if (lastUnderscoreIndex != -1 && lastDotIndex != -1 && lastDotIndex > lastUnderscoreIndex) {
        return url.substring(lastUnderscoreIndex + 1, lastDotIndex);
    }

    return ""; // Return empty string if the pattern is not found
}

void mqttCallback(String &topic, String &payload) {
    Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

    if (topic == mqtt_ConfigTopic) {
      sensorName = payload;
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
    wifiLib.connect();
  }
  mqttClientLib->connect({mqtt_ConfigTopic, mqtt_OTAtopic});
  Serial.println("MQTT Client is connected");
}

void setup() {
    pinMode(irLedPin, OUTPUT); // Initialize the LED pin as an output
    pinMode(irPhototransistorPin, INPUT);   // Initialize the IR pin as an input
    pinMode(ledPin, OUTPUT); // Initialize the LED pin as an output
    digitalWrite(irLedPin, LOW);
    digitalWrite(ledPin, LOW); 
    // Turn the LED off
    Serial.begin(115200);    // Start the Serial communication at 115200 baud rate
    Serial.print("SML Sensor ");
    Serial.println(version);

      // Get the high 2 bytes of the EFUSE MAC address, convert to hexadecimal, and append to the chipID String
    chipID += String((uint16_t)(ESP.getEfuseMac() >> 32), HEX);
    // Get the low 4 bytes of the EFUSE MAC address, convert to hexadecimal, and append to the chipID String
    chipID += String((uint32_t)ESP.getEfuseMac(), HEX);
    // Print the Chip ID
    Serial.print("ESP32 Chip ID: ");
    Serial.println(chipID);

    mqtt_ConfigTopic += chipID;

    // Connect to WiFi
    Serial.print("Connecting to WiFi ");
    wifiLib.scanAndSelectNetwork();
    wifiLib.connect();
    String ssid = wifiLib.getSSID();
    
    // Set up MQTT
    String mqttClientID = "ESP32SMLSensorClient_" + chipID;
    mqttClientLib = std::make_unique<MQTTClientLib>(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
    connectToMQTT();
    
    // Print the IP address
    Serial.print("IP Address: ");
    Serial.println(WiFi.localIP());

    serialPort.begin(9600, SWSERIAL_8N1, irPhototransistorPin, 0, false);
    if (!serialPort) { // If the object did not initialize, then its configuration is invalid
        Serial.println("Invalid EspSoftwareSerial pin configuration, check config"); 
        while (1) { // Don't continue with invalid configuration
            delay (1000);
        }
    } 
    //serialPort.onReceive(receiveHandler);
}

void loop() {
    // Read data from the serial port
    while (serialPort.available() > 0) {
        uint8_t data = serialPort.read();
        if (buffer.size() >= BUFFER_SIZE) {
            buffer.pop_front();  // Remove the oldest data
        }
        buffer.push_back(data);
        yield();
    }

    // Check if the buffer contains a complete burst
    if (buffer.size() >= startSequence.size() + endSequencePrefix.size() + 3) {
        bool startFound = false;
        bool endFound = false;
        int startIndex = -1;
        int endIndex = -1;

        // Find the start sequence
        for (size_t i = 0; i <= buffer.size() - startSequence.size(); ++i) {
            bool match = true;
            for (size_t j = 0; j < startSequence.size(); ++j) {
                if (buffer[i + j] != startSequence[j]) {
                    match = false;
                    break;
                }
            }
            if (match) {
                startIndex = i + startSequence.size();
                startFound = true;
                break;
            }
        }

        // Find the end sequence
        if (startFound) {
            for (size_t i = startIndex; i <= buffer.size() - endSequencePrefix.size() - 3; ++i) {
                bool match = true;
                for (size_t j = 0; j < endSequencePrefix.size(); ++j) {
                    if (buffer[i + j] != endSequencePrefix[j]) {
                        match = false;
                        break;
                    }
                }
                if (match) {
                    endIndex = i;
                    endFound = true;
                    break;
                }
            }
        }

        // If both start and end sequences are found, parse the data
        if (startFound && endFound) {
            std::vector<uint8_t> bufferVector(buffer.begin() + startIndex, buffer.begin() + endIndex);
            try {
                // Call the Parse method
                std::shared_ptr<SMLData> smlData = SMLParser::Parse(bufferVector);

                // Check if the parsing was successful
                if (smlData) {
                    // Output the parsed data
                    Serial.print("Tarif1: ");
                    Serial.print(smlData->Tarif1);
                    Serial.print(" kWh\t");
                    Serial.print("Tarif2: ");
                    Serial.print(smlData->Tarif2);
                    Serial.print(" kWh\t");
                    Serial.print("Power: ");
                    Serial.print(smlData->Power);
                    Serial.println(" W");

                    StaticJsonDocument<200> jsonDoc;
                    jsonDoc["Netzbezug"] = smlData->Tarif1;
                    jsonDoc["Netzeinspeissung"] = smlData->Tarif2;
                    jsonDoc["NetzanschlussMomentanleistung"] = smlData->Power;
                    
                    String jsonString;
                    serializeJson(jsonDoc, jsonString);
                    
                    mqttSuccess = mqttClientLib->publish((baseTopic + "/strom/M1/Zaehler").c_str(), jsonString, true, 0);
                    if (mqttSuccess) {
                      digitalWrite(ledPin, HIGH); 
                      delay(5);
                      digitalWrite(ledPin, LOW);// Turn on the LED if MQTT message was sent successfully
                    }
                } else {
                    //Serial.println("Parsing failed: No data returned");
                }
            } catch (const std::exception& ex) {
                // Handle any exceptions that occur during parsing
                Serial.print("Exception occurred: ");
                Serial.println(ex.what());
                Serial.println("Data: ");
                for (uint8_t byte : bufferVector) {
                    if (byte < 0x10) {
                      Serial.print("0");
                    }
                    Serial.print(byte, HEX);
                    Serial.print(" ");
                }
                Serial.println();
            }

            // Remove the processed data from the buffer
            buffer.erase(buffer.begin(), buffer.begin() + endIndex + endSequencePrefix.size() + 3);
        }
    }
}