#pragma once

#include <cstdint>
#include <Arduino.h>
#include <vector>

#include <SensirionI2cSht4x.h>
#include "ISensor.h"
#include <Wire.h>

#define SHT45_SCL 19
#define SHT45_SDA 20

class Sht45Sensor : public ISensor {
public:
    Sht45Sensor();
    Sht45Sensor(uint8_t SDAPin, uint8_t SCLPin);
    bool begin() override;
    std::vector<SensorData> read() override;

private:
    SensirionI2cSht4x sensor;
    uint8_t sdaPin = SHT45_SDA;
    uint8_t sclPin = SHT45_SCL;
    uint64_t sensorId = 0;
};
