#pragma once

#include <Arduino.h>
#include <cstdio>
#include "SensorData.h"
#include <vector>

class ISensor {
public:
    virtual ~ISensor() = default;
    virtual bool begin() = 0;
    virtual std::vector<SensorData> read() = 0;
    virtual String formatSensorId(uint64_t sensorId) const {
        char buffer[21];
        snprintf(buffer, sizeof(buffer), "0x%016llX", static_cast<unsigned long long>(sensorId));
        return String(buffer);
    }
};
