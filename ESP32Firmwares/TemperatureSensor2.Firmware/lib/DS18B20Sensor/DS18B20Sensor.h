#pragma once

#include <cstdint>
#include <Arduino.h>
#include <cstdio>

#include "ISensor.h"
#include <OneWire.h>

#define DATA_PIN 18

class DS18B20Sensor : public ISensor {
public:
    DS18B20Sensor();
    DS18B20Sensor(uint8_t dataPin);
    bool begin() override;
    SensorData read() override;

private:
    OneWire sensor;
    uint8_t dataPin = DATA_PIN;
    byte sensorAddress[8];
    byte sensorData[9];
    byte sensorType = 0;
};
