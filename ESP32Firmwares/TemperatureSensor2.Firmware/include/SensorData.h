#pragma once
#include <cstdint>

struct SensorData {
    float temperature = 0.0f;
    float humidity = 0.0f;
    uint32_t timestampMs = 0;
    bool success = false;
    uint64_t sensorId = 0;
};
