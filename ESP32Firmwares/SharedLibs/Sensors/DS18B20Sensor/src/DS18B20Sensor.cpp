#include "DS18B20Sensor.h"

DS18B20Sensor::DS18B20Sensor() {}
DS18B20Sensor::DS18B20Sensor(uint8_t dataPin) : dataPin(dataPin) {}

uint64_t DS18B20Sensor::toSensorId(const std::array<uint8_t, 8> &address)
{
    uint64_t id = 0;
    for (size_t i = 0; i < address.size(); ++i)
    {
        id |= static_cast<uint64_t>(address[i]) << (8 * i);
    }
    return id;
}

bool DS18B20Sensor::begin()
{
    sensor.begin(dataPin);
    sensor.reset_search();
    sensorAddresses.clear();

    std::array<uint8_t, 8> address{};
    while (sensor.search(address.data()) == 1)
    {
        if (OneWire::crc8(address.data(), 7) != address[7])
        {
            Serial.println("Found sensor with invalid CRC, skipping device.");
            continue;
        }

        const char *chipName = nullptr;
        bool firstSensor = sensorAddresses.empty();

        switch (address[0])
        {
        case 0x10:
            chipName = "DS18S20";
            break;
        case 0x28:
            chipName = "DS18B20";
            break;
        case 0x22:
            chipName = "DS1822";
            break;
        default:
            Serial.println("Unknown sensor family, skipping device.");
            continue;
        }

        sensorAddresses.push_back(address);

        if (firstSensor)
        {
            sensorType = (address[0] == 0x10) ? 1 : address[0];
        }

        Serial.print("Found sensor: ");
        Serial.print(chipName);
        Serial.print(" at address ");
        for (size_t i = 0; i < address.size(); i++)
        {
            if (i > 0)
            {
                Serial.print(":");
            }
            Serial.print(address[i], HEX);
        }
        Serial.println();
    }

    sensor.reset_search();

    if (!sensorAddresses.empty())
    {
        return true;
    }

    Serial.println("No DS18B20 sensor found on the bus.");
    return false;
}

std::vector<SensorData> DS18B20Sensor::read()
{
    std::vector<SensorData> results;

    if (sensorAddresses.empty())
    {
        SensorData data{};
        Serial.println("No DS18B20 sensor address available for reading.");
        data.success = false;
        results.push_back(data);
        return results;
    }

    sensor.reset();
    sensor.skip();
    sensor.write(0x44, 1); // start conversion for all devices, parasite power on at the end

    delay(750);

    for (const auto &address : sensorAddresses)
    {
        SensorData data{};
        data.sensorId = toSensorId(address);

        if (sensor.reset() == 0)
        {
            Serial.println("Sensor not responding during read!");
            data.success = false;
            results.push_back(data);
            continue;
        }

        sensor.select(address.data());
        sensor.write(0xBE); // read scratchpad

        for (byte i = 0; i < 9; i++)
        {
            sensorData[i] = sensor.read();
        }

        if (OneWire::crc8(sensorData, 8) != sensorData[8])
        {
            Serial.println("Scratchpad CRC mismatch, skipping reading.");
            data.success = false;
            results.push_back(data);
            continue;
        }

        int16_t raw = (sensorData[1] << 8) | sensorData[0];
        bool isDS18S20 = (address[0] == 0x10);

        if (isDS18S20)
        {
            raw <<= 3; // 9 bit resolution default for DS18S20
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
            }
            else if (cfg == 0x20)
            {
                raw &= ~3;
            }
            else if (cfg == 0x40)
            {
                raw &= ~1;
            }
        }

        float celsius = static_cast<float>(raw) / 16.0f;
        data.temperature = celsius;
        data.humidity = NAN; // DS18B20 does not provide humidity
        data.timestampMs = millis();
        data.success = true;
        results.push_back(data);
    }

    return results;
}

String DS18B20Sensor::formatSensorId(uint64_t sensorId) const
{
    char buffer[24];
    size_t index = 0;
    for (int byteIndex = 0; byteIndex < 8; ++byteIndex)
    {
        uint8_t byteValue = static_cast<uint8_t>((sensorId >> (8 * byteIndex)) & 0xFF);
        if (byteIndex > 0)
        {
            buffer[index++] = '-';
        }
        uint8_t highNibble = (byteValue >> 4) & 0x0F;
        uint8_t lowNibble = byteValue & 0x0F;
        buffer[index++] = highNibble < 10 ? ('0' + highNibble) : ('A' + highNibble - 10);
        buffer[index++] = lowNibble < 10 ? ('0' + lowNibble) : ('A' + lowNibble - 10);
    }
    buffer[index] = '\0';
    return String(buffer);
}
