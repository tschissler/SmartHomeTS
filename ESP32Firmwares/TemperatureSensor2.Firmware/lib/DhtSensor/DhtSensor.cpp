#include "DhtSensor.h"

#include <Arduino.h>
#include <cmath>

DhtSensor::DhtSensor(uint8_t pin, uint8_t type) : dht_(pin, type) {}

bool DhtSensor::begin() {
    dht_.begin();
    return true;
}

SensorData DhtSensor::read() {
    SensorData data{};
    data.humidity = dht_.readHumidity();
    data.temperature = dht_.readTemperature();

    if (isnan(data.humidity) || isnan(data.temperature)) {
        data.success = false;
        return data;
    }

    data.timestampMs = millis();
    data.success = true;
    return data;
}
