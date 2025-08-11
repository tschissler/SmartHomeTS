#ifndef THERMOSTAT_DATA_H
#define THERMOSTAT_DATA_H

#include <Arduino.h>
#include <ArduinoJson.h>

class ThermostatData {
private:
    float targetTemperature;
    float currentTemperature;
    int valvePosition;
    int batteryLevel;
    bool isConnected;
    String lastUpdated;
    bool isValid;

public:
    // Constructor
    ThermostatData();
    
    // Parse JSON payload
    bool parseFromJson(const String& jsonPayload);
    bool parseFromJsonDocument(const JsonDocument& doc);
    
    // Getters
    float getTargetTemperature() const { return targetTemperature; }
    float getCurrentTemperature() const { return currentTemperature; }
    int getValvePosition() const { return valvePosition; }
    int getBatteryLevel() const { return batteryLevel; }
    bool getIsConnected() const { return isConnected; }
    String getLastUpdated() const { return lastUpdated; }
    bool getIsValid() const { return isValid; }
    
    // Setters (if needed)
    void setTargetTemperature(float temp) { targetTemperature = temp; }
    void setCurrentTemperature(float temp) { currentTemperature = temp; }
    void setValvePosition(int position) { valvePosition = position; }
    void setBatteryLevel(int level) { batteryLevel = level; }
    void setIsConnected(bool connected) { isConnected = connected; }
    void setLastUpdated(const String& updated) { lastUpdated = updated; }
    
    // Utility methods
    void reset();
    String toString() const;
    void printToSerial() const;
};

#endif // THERMOSTAT_DATA_H
