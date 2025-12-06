#pragma once

#include <cstdint>
#include <vector>

#include "DHT.h"
#include "ISensor.h"

#define DHTPIN 19 
#define DHTTYPE DHT22  

class DhtSensor : public ISensor {
public:
    DhtSensor(uint8_t pin, uint8_t type);
    bool begin() override;
    std::vector<SensorData> read() override;

private:
    DHT dht_;
};
