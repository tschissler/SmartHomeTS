#include <Arduino.h>
#include <WiFi.h>
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>
#include <ESP32-TWAI-CAN.hpp>
#include <ArduinoJson.h>

#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"
#include "ESP32Helpers.h"
#include "soc/soc.h"
#include "soc/rtc_cntl_reg.h"

// Pin configuration - define your CAN bus pins here
#define CAN_RX_PIN 22
#define CAN_TX_PIN 23

CanFrame rxFrame;

const char* version = "0.0.1"; // Initial version
String chipID = "";
 
// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);

WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP);
MQTTClientLib* mqttClientLib = nullptr;

static int otaInProgress = 0;
static bool otaEnable = true;
static bool sendMQTTMessages = true;
static bool mqttSuccess = false;

static String baseTopic = "data";
static String sensorName = "HovalWP_M3";
const String mqtt_broker = "smarthomepi2";
static String mqtt_OTAtopic = "OTAUpdate/CanBusGateway";
static String mqtt_ConfigTopic = "config/CanBusGateway/{ID}/DeviceName";

unsigned long lastDataPublishTime = 0;
const unsigned long DATA_PUBLISH_INTERVAL = 60000; // Publish data every minute

// Debug mode - set to true to log all CAN messages
bool debugMode = true;

// Heat pump data structure based on Hoval datapoints
struct HovalData {
  // System temperatures
  float outsideTemp = 0;            // Outside temperature (°C)
  float flowTemp = 0;               // Flow temperature (°C)
  float returnTemp = 0;             // Return temperature (°C)
  float dhwTemp = 0;                // DHW temperature (°C)
  float flowTempSetpoint = 0;       // Flow temperature setpoint (°C)
  float dhwTempSetpoint = 0;        // DHW temperature setpoint (°C)
  
  // Heat pump specific
  float hpFlowTemp = 0;             // Heat pump flow temperature (°C)
  float hpReturnTemp = 0;           // Heat pump return temperature (°C)
  float compressorModulation = 0;   // Compressor modulation (%)
  bool compressorStatus = false;    // Compressor status (on/off)
  float electricalPower = 0;        // Electrical power consumption (kW)
  float thermalPower = 0;           // Thermal power output (kW)
  float cop = 0;                    // COP value
  
  // Operation parameters
  int operatingMode = 0;            // Current operating mode
  int heatPumpState = 0;            // Heat pump state
  float roomTempSetpoint = 0;       // Room temperature setpoint (°C)
  
  // Error status
  bool hasError = false;            // Error status
  int errorCode = 0;                // Error code
  
  // Energy counters
  float heatingEnergyTotal = 0;     // Total heating energy (kWh)
  float dhwEnergyTotal = 0;         // Total DHW energy (kWh)
  float electricalEnergyTotal = 0;  // Total electrical energy (kWh)
};

HovalData hovalData;

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
      Serial.println("Device name set to: " + sensorName);
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
          bool result = AzureOTAUpdater::UpdateFirmwareFromUrl(firmwareUrl);
          if (result) {
            Serial.println("OTA Update successfully initiated, waiting to be finished");
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

void setupCanBus() {
  // Initialize CAN Bus communication
  Serial.println("Setting up CAN Bus...");
  
  // Configure CAN bus
  ESP32Can.setPins(CAN_TX_PIN, CAN_RX_PIN);
  ESP32Can.setSpeed(TWAI_SPEED_125KBPS); // Set to 125 kbps, common for HVAC systems
  if(ESP32Can.begin()) {
  Serial.println("CAN Bus initialized at 125 kbps");
  } else {
    Serial.println("Failed to initialize CAN Bus");
    return;
  }
  
  // If you're not receiving messages, you might need to try different speeds
  // Common speeds for HVAC systems: 125kbps, 250kbps, 500kbps
}

// Function to decode a Hoval value from CAN data
float decodeHovalValue(const uint8_t *data, int startByte, float factor = 1.0) {
  // Hoval values are typically stored as 16-bit integers (MSB first)
  int16_t rawValue = (data[startByte] << 8) | data[startByte + 1];
  return (float)rawValue * factor;
}

// Function to decode Hoval heat pump data from CAN frames
void decodeHovalData(const CanFrame &frame) {
  // Based on Hoval TopTronic E datapoints spreadsheet
  
  switch(frame.identifier) {
    // Group 1: Basic system temperatures (typically 0x180 + node ID)
    case 0x181: // Outside temperature
      if (frame.data_length_code >= 2) {
        hovalData.outsideTemp = decodeHovalValue(frame.data, 0, 0.1);
      }
      break;
    
    case 0x182: // Flow temperature
      if (frame.data_length_code >= 2) {
        hovalData.flowTemp = decodeHovalValue(frame.data, 0, 0.1);
      }
      break;
    
    case 0x183: // Return temperature
      if (frame.data_length_code >= 2) {
        hovalData.returnTemp = decodeHovalValue(frame.data, 0, 0.1);
      }
      break;
    
    case 0x184: // DHW temperature
      if (frame.data_length_code >= 2) {
        hovalData.dhwTemp = decodeHovalValue(frame.data, 0, 0.1);
      }
      break;
    
    // Group 2: Setpoints (typically 0x200 + node ID)
    case 0x201: // Flow temperature setpoint
      if (frame.data_length_code >= 2) {
        hovalData.flowTempSetpoint = decodeHovalValue(frame.data, 0, 0.1);
      }
      break;
    
    case 0x202: // DHW temperature setpoint
      if (frame.data_length_code >= 2) {
        hovalData.dhwTempSetpoint = decodeHovalValue(frame.data, 0, 0.1);
      }
      break;
    
    // Group 3: Heat pump specific (typically 0x300 + node ID)
    case 0x301: // Heat pump flow temperature
      if (frame.data_length_code >= 2) {
        hovalData.hpFlowTemp = decodeHovalValue(frame.data, 0, 0.1);
      }
      break;
    
    case 0x302: // Heat pump return temperature
      if (frame.data_length_code >= 2) {
        hovalData.hpReturnTemp = decodeHovalValue(frame.data, 0, 0.1);
      }
      break;
    
    case 0x310: // Compressor modulation
      if (frame.data_length_code >= 2) {
        hovalData.compressorModulation = decodeHovalValue(frame.data, 0, 0.1);
      }
      break;
    
    // Group 4: Power and energy data (typically 0x400 + node ID)
    case 0x401: // Electrical power
      if (frame.data_length_code >= 2) {
        hovalData.electricalPower = decodeHovalValue(frame.data, 0, 0.01);
      }
      break;
    
    case 0x402: // Thermal power
      if (frame.data_length_code >= 2) {
        hovalData.thermalPower = decodeHovalValue(frame.data, 0, 0.1);
      }
      break;
    
    case 0x403: // COP
      if (frame.data_length_code >= 2) {
        hovalData.cop = decodeHovalValue(frame.data, 0, 0.01);
      }
      break;
    
    // Group 5: Status data (typically 0x500 + node ID)
    case 0x501: // Operating mode
      if (frame.data_length_code >= 1) {
        hovalData.operatingMode = frame.data[0];
      }
      break;
    
    case 0x502: // Heat pump state
      if (frame.data_length_code >= 1) {
        hovalData.heatPumpState = frame.data[0];
        // Bit 0 is typically compressor status
        hovalData.compressorStatus = (frame.data[0] & 0x01) > 0;
      }
      break;
    
    case 0x510: // Error status
      if (frame.data_length_code >= 1) {
        hovalData.hasError = frame.data[0] > 0;
      }
      break;
    
    case 0x511: // Error code
      if (frame.data_length_code >= 2) {
        hovalData.errorCode = (frame.data[0] << 8) | frame.data[1];
      }
      break;
    
    // Group 6: Energy counters (typically 0x600 + node ID)
    case 0x601: // Heating energy total
      if (frame.data_length_code >= 4) {
        // Energy counters are often 32-bit values
        uint32_t energy = (frame.data[0] << 24) | (frame.data[1] << 16) | 
                           (frame.data[2] << 8) | frame.data[3];
        hovalData.heatingEnergyTotal = energy * 0.1;
      }
      break;
    
    case 0x602: // DHW energy total
      if (frame.data_length_code >= 4) {
        uint32_t energy = (frame.data[0] << 24) | (frame.data[1] << 16) | 
                           (frame.data[2] << 8) | frame.data[3];
        hovalData.dhwEnergyTotal = energy * 0.1;
      }
      break;
    
    case 0x603: // Electrical energy total
      if (frame.data_length_code >= 4) {
        uint32_t energy = (frame.data[0] << 24) | (frame.data[1] << 16) | 
                           (frame.data[2] << 8) | frame.data[3];
        hovalData.electricalEnergyTotal = energy * 0.1;
      }
      break;
  }
}

// Add this mapping before the processCanMessages function

// Map CAN IDs to human-readable names for debug
String getCanIdName(uint32_t canId) {
  switch(canId) {
    case 0x181: return "OutsideTemp";
    case 0x182: return "FlowTemp";
    case 0x183: return "ReturnTemp";
    case 0x184: return "DHWTemp";
    case 0x201: return "FlowTempSetpoint";
    case 0x202: return "DHWTempSetpoint";
    case 0x301: return "HPFlowTemp";
    case 0x302: return "HPReturnTemp";
    case 0x310: return "CompressorModulation";
    case 0x401: return "ElectricalPower";
    case 0x402: return "ThermalPower";
    case 0x403: return "COP";
    case 0x501: return "OperatingMode";
    case 0x502: return "HPState";
    case 0x510: return "ErrorStatus";
    case 0x511: return "ErrorCode";
    case 0x601: return "HeatingEnergyTotal";
    case 0x602: return "DHWEnergyTotal";
    case 0x603: return "ElectricalEnergyTotal";
    default: return "Unknown";
  }
}

// Add helper functions for interpreting status values

String getOperatingModeName(int mode) {
  switch(mode) {
    case 0: return "Standby";
    case 1: return "Heating";
    case 2: return "Cooling";
    case 3: return "DHW";
    case 4: return "Manual";
    case 5: return "Emergency";
    default: return "Unknown";
  }
}

String getHeatPumpStateName(int state) {
  switch(state) {
    case 0: return "Off";
    case 1: return "Standby";
    case 2: return "Heating";
    case 3: return "DHW";
    case 4: return "Cooling";
    case 5: return "Defrost";
    case 6: return "Error";
    default: return "Unknown";
  }
}


// Function to publish heat pump data to MQTT
void publishHovalData() {
  // Create a JSON document for the data
  StaticJsonDocument<1024> jsonDoc;
  
  // Basic system temperatures
  jsonDoc["outside_temp"] = hovalData.outsideTemp;
  jsonDoc["flow_temp"] = hovalData.flowTemp;
  jsonDoc["return_temp"] = hovalData.returnTemp;
  jsonDoc["dhw_temp"] = hovalData.dhwTemp;
  
  // Setpoints
  jsonDoc["flow_temp_setpoint"] = hovalData.flowTempSetpoint;
  jsonDoc["dhw_temp_setpoint"] = hovalData.dhwTempSetpoint;
  
  // Heat pump specific
  jsonDoc["hp_flow_temp"] = hovalData.hpFlowTemp;
  jsonDoc["hp_return_temp"] = hovalData.hpReturnTemp;
  jsonDoc["compressor_modulation"] = hovalData.compressorModulation;
  jsonDoc["compressor_status"] = hovalData.compressorStatus;
  jsonDoc["electrical_power"] = hovalData.electricalPower;
  jsonDoc["thermal_power"] = hovalData.thermalPower;
  jsonDoc["cop"] = hovalData.cop;
  
  // Status and operation
  jsonDoc["operating_mode"] = hovalData.operatingMode;
  jsonDoc["operating_mode_text"] = getOperatingModeName(hovalData.operatingMode);
  jsonDoc["heat_pump_state"] = hovalData.heatPumpState;
  jsonDoc["heat_pump_state_text"] = getHeatPumpStateName(hovalData.heatPumpState);
  
  // Error information
  jsonDoc["has_error"] = hovalData.hasError;
  if (hovalData.hasError) {
    jsonDoc["error_code"] = hovalData.errorCode;
  }
  
  // Energy counters
  jsonDoc["heating_energy_total"] = hovalData.heatingEnergyTotal;
  jsonDoc["dhw_energy_total"] = hovalData.dhwEnergyTotal;
  jsonDoc["electrical_energy_total"] = hovalData.electricalEnergyTotal;
  
  // Performance metrics
  // Add calculated COP if we have both power values and electrical power is not zero
  if (hovalData.electricalPower > 0 && hovalData.thermalPower > 0) {
    float calculatedCOP = hovalData.thermalPower / hovalData.electricalPower;
    if (hovalData.cop == 0) { // If we don't have a direct COP reading
      jsonDoc["calculated_cop"] = calculatedCOP;
    }
  }
  
  // Serialize JSON to string
  String jsonString;
  serializeJson(jsonDoc, jsonString);
  
  // Publish to MQTT
  String topic = baseTopic + "/hoval/status";
  mqttClientLib->publish(topic.c_str(), jsonString, true, 0); // Retained message with QoS 0
    
  Serial.println("Published Hoval data to MQTT");
}


// Update the processCanMessages function
void processCanMessages() {
  
  // Check if CAN messages are available
  if (ESP32Can.readFrame(rxFrame, 1000)) {
    // Log raw CAN frame for debugging
    if (debugMode) {
      String idName = getCanIdName(rxFrame.identifier);
      
      Serial.print("CAN frame: 0x");
      Serial.print(rxFrame.identifier, HEX);
      Serial.print(" (");
      Serial.print(idName);
      Serial.print(") Data: ");
      
      for (int i = 0; i < rxFrame.data_length_code; i++) {
        if (rxFrame.data[i] < 16) Serial.print("0");
        Serial.print(rxFrame.data[i], HEX);
        Serial.print(" ");
      }
      Serial.println();
    }
    
    // Decode the Hoval heat pump data
    decodeHovalData(rxFrame);
    
    // Publish raw CAN message for analysis
    if (mqttClientLib) {
      String idName = getCanIdName(rxFrame.identifier);
      String rawTopic = baseTopic + "/hoval/raw/" + String(rxFrame.identifier, HEX);
      
      // Create a more descriptive payload with ID name
      StaticJsonDocument<256> jsonDoc;
      jsonDoc["id"] = "0x" + String(rxFrame.identifier, HEX);
      jsonDoc["name"] = idName;
      
      // Add raw data bytes
      JsonArray dataArray = jsonDoc.createNestedArray("data");
      for (int i = 0; i < rxFrame.identifier; i++) {
        dataArray.add(rxFrame.data[i]);
      }
      
      // Add decoded value if possible
      if (rxFrame.data_length_code >= 2) {
        float value = decodeHovalValue(rxFrame.data, 0, 0.1);
        jsonDoc["value"] = value;
      }
      
      String jsonString;
      serializeJson(jsonDoc, jsonString);
      mqttClientLib->publish(rawTopic.c_str(), jsonString, false, 0);
      
      // Also publish to a more user-friendly topic if we know what this ID is
      if (idName != "Unknown") {
        String friendlyTopic = baseTopic + "/hoval/raw/" + idName;
        mqttClientLib->publish(friendlyTopic.c_str(), jsonString, false, 0);
      }
    }
  }
  
  // Periodically publish aggregated heat pump data
  unsigned long currentMillis = millis();
  if (currentMillis - lastDataPublishTime >= DATA_PUBLISH_INTERVAL) {
    publishHovalData();
    lastDataPublishTime = currentMillis;
  }
}

void setup() {
  WRITE_PERI_REG(RTC_CNTL_BROWN_OUT_REG, 0); // Disable brownout detector
  Serial.begin(115200);
  Serial.print("CanBusGateway version ");
  Serial.println(version);

  chipID = ESP32Helpers::getChipId();
  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);
  
  mqtt_ConfigTopic.replace("{ID}", chipID);

  // Connect to WiFi
  Serial.println("Connecting to WiFi...");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();
  String ssid = wifiLib.getSSID();
  Serial.println("Connected to WiFi SSID: " + ssid);

  // Set up MQTT
  String mqttClientID = "ESP32CanBusGatewayClient_" + chipID;
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT();

  // Initialize NTPClient
  timeClient.begin();
  timeClient.setTimeOffset(0); // Set your time offset from UTC in seconds
  timeClient.update();
  
  // Setup CAN Bus
  setupCanBus();

  // Publish device info to MQTT
  mqttClientLib->publish(("meta/" + sensorName + "/version/CanBusGateway").c_str(), String(version), true, 2);
  
  Serial.println("Setup complete");
}

void loop() {
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();

  if (otaInProgress < 0) {
    Serial.println("OTA update failed");
  }

  if (otaInProgress != 1) {
    timeClient.update();

    // Process CAN messages
    processCanMessages();

    // Check MQTT connection
    if (!mqttClientLib->loop()) {
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT();
    }
  }
  
  delay(10); // Small delay to prevent CPU overload
}