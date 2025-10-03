#ifndef SENSOR_CONFIG_H
#define SENSOR_CONFIG_H

// GPIO Pin Definitions - Boot-safe pins
#define SHTC3_SDA_PIN   22
#define SHTC3_SCL_PIN   21

#define BMP180_SDA_PIN  19
#define BMP180_SCL_PIN  18

#define BME280_SDA_PIN  16
#define BME280_SCL_PIN  17

// I2C Addresses
#define BME280_ADDRESS_1  0x76
#define BME280_ADDRESS_2  0x77

// Sensor capability flags
#define SHTC3_HAS_TEMPERATURE   true
#define SHTC3_HAS_HUMIDITY      true
#define SHTC3_HAS_PRESSURE      false

#define BMP180_HAS_TEMPERATURE  true
#define BMP180_HAS_HUMIDITY     false
#define BMP180_HAS_PRESSURE     true

#define BME280_HAS_TEMPERATURE  true
#define BME280_HAS_HUMIDITY     true
#define BME280_HAS_PRESSURE     true

// Measurement intervals
#define READING_INTERVAL_MS     5000

#endif // SENSOR_CONFIG_H