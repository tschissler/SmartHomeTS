#pragma once

#include "SensorData.h"

class ISensor {
public:
    virtual ~ISensor() = default;
    virtual bool begin() = 0;
    virtual SensorData read() = 0;
};
