#pragma once

#include <cstdint>
#include <Arduino.h>
#include <cstdio>
#include <array>
#include <vector>

#include "ISensor.h"
#include <OneWire.h>

#define DATA_PIN 18

class DS18B20Sensor : public ISensor {
public:
    DS18B20Sensor();
    DS18B20Sensor(uint8_t dataPin);
    bool begin() override;
    std::vector<SensorData> read() override;
    String formatSensorId(uint64_t sensorId) const override;

private:
    OneWire sensor;
    uint8_t dataPin = DATA_PIN;
    std::vector<std::array<uint8_t, 8>> sensorAddresses;
    byte sensorData[9];
    byte sensorType = 0;
    static uint64_t toSensorId(const std::array<uint8_t, 8>& address);
};
