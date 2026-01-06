#pragma once

#include <cstdint>
#include <Arduino.h>
#include <vector>

#include <Wire.h>
#include <Adafruit_BMP280.h>
#include <Adafruit_BME280.h>

#include "ISensor.h"

#define BMP280_SDA 21
#define BMP280_SCL 22

class Bmp280Sensor : public ISensor {
public:
    Bmp280Sensor();
    Bmp280Sensor(uint8_t SDAPin, uint8_t SCLPin);

    bool begin() override;
    std::vector<SensorData> read() override;

private:
    Adafruit_BMP280 bmp;
    Adafruit_BME280 bme;
    bool isBme = false;
    uint8_t sdaPin = BMP280_SDA;
    uint8_t sclPin = BMP280_SCL;
    uint64_t sensorId = 0;
};
