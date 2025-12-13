#include "Sht45Sensor.h"

Sht45Sensor::Sht45Sensor() {}
Sht45Sensor::Sht45Sensor(uint8_t SDAPin, uint8_t SCLPin) {
    sdaPin = SDAPin;
    sclPin = SCLPin;
}

bool Sht45Sensor::begin() {
    Wire.begin(sdaPin, sclPin);
    sensor.begin(Wire, SHT40_I2C_ADDR_44);
    uint32_t serial = 0;
    if (sensor.serialNumber(serial) == 0) {
        sensorId = serial;
    } else {
        sensorId = 0;
    }
    return true;
}

std::vector<SensorData> Sht45Sensor::read() {
    std::vector<SensorData> result;
    SensorData data{};
    float temperature = 0.0f;
    float humidity = 0.0f;

    int16_t error = sensor.measureHighPrecision(temperature, humidity);
    if (error != 0) {
        data.success = false;
        result.push_back(data);
        return result;
    }

    data.temperature = temperature;
    data.humidity = humidity;
    data.timestampMs = millis();
    data.success = true;
    data.sensorId = sensorId;
    result.push_back(data);
    return result;
}
