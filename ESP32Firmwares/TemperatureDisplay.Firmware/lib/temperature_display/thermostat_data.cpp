#include "thermostat_data.h"
#include <time.h>

ThermostatData::ThermostatData() {
    reset();
}

void ThermostatData::reset() {
    targetTemperature = 0.0f;
    currentTemperature = 0.0f;
    valvePosition = 0;
    batteryLevel = 0;
    isConnected = false;
    lastUpdated = "";
    isValid = false;
}

bool ThermostatData::parseFromJson(const String& jsonPayload) {
    StaticJsonDocument<512> doc;
    DeserializationError error = deserializeJson(doc, jsonPayload);
    
    if (error) {
        Serial.printf("Failed to parse JSON: %s\n", error.c_str());
        isValid = false;
        return false;
    }
    
    return parseFromJsonDocument(doc);
}

bool ThermostatData::parseFromJsonDocument(const JsonDocument& doc) {
    // Reset current data
    reset();
    
    try {
        // Parse required fields
        if (doc.containsKey("TargetTemperature")) {
            targetTemperature = doc["TargetTemperature"].as<float>();
        } else {
            Serial.println("Missing TargetTemperature field");
            return false;
        }
        
        if (doc.containsKey("CurrentTemperature")) {
            currentTemperature = doc["CurrentTemperature"].as<float>();
        } else {
            Serial.println("Missing CurrentTemperature field");
            return false;
        }
        
        if (doc.containsKey("ValvePosition")) {
            valvePosition = doc["ValvePosition"].as<int>();
        } else {
            Serial.println("Missing ValvePosition field");
            return false;
        }
        
        if (doc.containsKey("BatteryLevel")) {
            batteryLevel = doc["BatteryLevel"].as<int>();
        } else {
            Serial.println("Missing BatteryLevel field");
            return false;
        }
        
        if (doc.containsKey("IsConnected")) {
            isConnected = doc["IsConnected"].as<bool>();
        } else {
            Serial.println("Missing IsConnected field");
            return false;
        }
        
        if (doc.containsKey("LastUpdated")) {
            lastUpdated = doc["LastUpdated"].as<String>();
        } else {
            Serial.println("Missing LastUpdated field");
            return false;
        }
        
        isValid = true;
        return true;
        
    } catch (...) {
        Serial.println("Exception while parsing JSON fields");
        isValid = false;
        return false;
    }
}

String ThermostatData::toString() const {
    if (!isValid) {
        return "Invalid ThermostatData";
    }
    
    String result = "ThermostatData {\n";
    result += "  TargetTemperature: " + String(targetTemperature, 1) + "°C\n";
    result += "  CurrentTemperature: " + String(currentTemperature, 1) + "°C\n";
    result += "  ValvePosition: " + String(valvePosition) + "%\n";
    result += "  BatteryLevel: " + String(batteryLevel) + "%\n";
    result += "  IsConnected: " + String(isConnected ? "true" : "false") + "\n";
    result += "  LastUpdated: " + lastUpdated + "\n";
    result += "}";
    return result;
}

void ThermostatData::printToSerial() const {
    Serial.println(toString());
}