#include "DS18B20Sensor.h"

DS18B20Sensor::DS18B20Sensor() {}
DS18B20Sensor::DS18B20Sensor(uint8_t dataPin)
{
    dataPin = dataPin;
}

bool DS18B20Sensor::begin()
{
    sensor.begin(dataPin);
    sensor.reset_search();
    if (sensor.search(sensorAddress) == 1)
    {
        const char *chipName = nullptr;

        switch (sensorAddress[0])
        {
        case 0x10:
            chipName = "DS18S20";
            sensorType = 1;
            break;
        case 0x28:
            chipName = "DS18B20";
            break;
        case 0x22:
            chipName = "DS1822";
            break;
        default:
            Serial.println("Unknown sensor family, skipping device.");
            return false;
        }
        Serial.print("Found sensor: ");
        Serial.print(chipName);

        sensorType = sensorAddress[0];
        return true;
    }
    Serial.println("No DS18B20 sensor found on the bus.");
    return false;
}

SensorData DS18B20Sensor::read()
{
    SensorData data{};
    float temperature = 0.0f;
    float humidity = 0.0f;

    sensor.reset();
    sensor.select(sensorAddress);
    sensor.write(0x44, 1); // start conversion, parasite power on at the end

    delay(750);

    sensor.reset();
    sensor.select(sensorAddress);
    sensor.write(0xBE); // read scratchpad

    for (byte i = 0; i < 9; i++)
    {
        sensorData[i] = sensor.read();
    }

    int16_t raw = (sensorData[1] << 8) | sensorData[0];
    uint8_t resolutionBits = 12;
    if (sensorType == 1)
    {
        raw <<= 3; // 9 bit resolution default for DS18S20
        resolutionBits = 9;
        if (sensorData[7] == 0x10)
        {
            raw = (raw & 0xFFF0) + 12 - sensorData[6];
        }
    }
    else
    {
        byte cfg = (sensorData[4] & 0x60);
        if (cfg == 0x00)
        {
            raw &= ~7;
            resolutionBits = 9;
        }
        else if (cfg == 0x20)
        {
            raw &= ~3;
            resolutionBits = 10;
        }
        else if (cfg == 0x40)
        {
            raw &= ~1;
            resolutionBits = 11;
        }
        else
        {
            resolutionBits = 12;
        }
    }

    float celsius = static_cast<float>(raw) / 16.0f;
    data.temperature = celsius;
    data.humidity = NAN; // DS18B20 does not provide humidity
    data.timestampMs = millis();
    data.success = true;
    return data;
}