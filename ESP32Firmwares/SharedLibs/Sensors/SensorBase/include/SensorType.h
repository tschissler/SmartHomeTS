#include <cstdint>
#pragma once
enum class SensorType : uint8_t {
    DHT22,
    SHT45,
    DS18B20
};

constexpr const char* toString(SensorType type) {
    switch (type) {
        case SensorType::DHT22:   return "DHT22";
        case SensorType::SHT45:  return "SHT45";
        case SensorType::DS18B20: return "DS18B20";
        default:                  return "Unknown";
    }
}
