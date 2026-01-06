#pragma once
#include <cstdint>
#include <cmath>

struct SensorData {
    float temperature = 0.0f;
    float humidity = 0.0f;
    float pressure = NAN; // hPa; NAN if not available
    uint32_t timestampMs = 0;
    bool success = false;
    uint64_t sensorId = 0;
};
