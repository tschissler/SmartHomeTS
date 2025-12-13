#include "DhtSensor.h"

#include <Arduino.h>
#include <cmath>

DhtSensor::DhtSensor(uint8_t pin, uint8_t type) : dht_(pin, type) {}

bool DhtSensor::begin() {
    dht_.begin();
    return true;
}

std::vector<SensorData> DhtSensor::read() {
    std::vector<SensorData> result;
    SensorData data{};
    data.humidity = dht_.readHumidity();
    data.temperature = dht_.readTemperature();

    if (isnan(data.humidity) || isnan(data.temperature)) {
        data.success = false;
        result.push_back(data);
        return result;
    }

    data.timestampMs = millis();
    data.success = true;
    data.sensorId = 0;
    result.push_back(data);
    return result;
}
