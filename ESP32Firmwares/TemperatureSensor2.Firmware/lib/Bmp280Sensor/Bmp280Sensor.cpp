#include "Bmp280Sensor.h"

Bmp280Sensor::Bmp280Sensor() {}
Bmp280Sensor::Bmp280Sensor(uint8_t SDAPin, uint8_t SCLPin) {
    sdaPin = SDAPin;
    sclPin = SCLPin;
}

bool Bmp280Sensor::begin() {
    // ESP32-C6 I2C (ng) can error if Wire.begin is called repeatedly without Wire.end.
    Wire.end();
    delay(5);
    Wire.begin(sdaPin, sclPin, 100000);

    // Many breakouts are labeled "BME/BMP280" but may actually contain either chip.
    // Try BME280 first (it can read humidity), then fall back to BMP280.
    bool ok = false;
    isBme = false;

    ok = bme.begin(0x76, &Wire);
    if (!ok) {
        ok = bme.begin(0x77, &Wire);
    }
    if (ok) {
        isBme = true;
        sensorId = static_cast<uint64_t>(bme.sensorID());
        return true;
    }

    ok = bmp.begin(0x76);
    if (!ok) {
        ok = bmp.begin(0x77);
    }

    if (!ok) {
        sensorId = 0;
        Wire.end();
        return false;
    }

    bmp.setSampling(
        Adafruit_BMP280::MODE_NORMAL,
        Adafruit_BMP280::SAMPLING_X2,
        Adafruit_BMP280::SAMPLING_X16,
        Adafruit_BMP280::FILTER_X16,
        Adafruit_BMP280::STANDBY_MS_500);

    sensorId = static_cast<uint64_t>(bmp.sensorID());
    return true;
}

std::vector<SensorData> Bmp280Sensor::read() {
    std::vector<SensorData> result;
    SensorData data{};

    float temperature = NAN;
    float humidity = NAN;
    float pressurePa = NAN;

    if (isBme) {
        temperature = bme.readTemperature();
        humidity = bme.readHumidity();
        pressurePa = bme.readPressure();
    } else {
        temperature = bmp.readTemperature();
        pressurePa = bmp.readPressure();
    }

    if (isnan(temperature) || isnan(pressurePa) || pressurePa <= 0.0f) {
        data.success = false;
        result.push_back(data);
        return result;
    }

    data.temperature = temperature;
    data.humidity = humidity;
    data.pressure = pressurePa / 100.0f; // Pa -> hPa
    data.timestampMs = millis();
    data.success = true;
    data.sensorId = sensorId;

    result.push_back(data);
    return result;
}
