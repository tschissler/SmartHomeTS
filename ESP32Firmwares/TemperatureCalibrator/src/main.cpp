#include <Arduino.h>
#include <Wire.h>
#include "Adafruit_SHTC3.h"
#include "Adafruit_BMP085.h"
#include "Adafruit_BME280.h"
#include "sensor_config.h"

// Create sensor objects
Adafruit_SHTC3 shtc3;
Adafruit_BMP085 bmp180;
Adafruit_BME280 bme280;

// Sensor status flags
bool shtc3_available = false;
bool bmp180_available = false;
bool bme280_available = false;

// Define multiple I2C interfaces
TwoWire I2C_1 = TwoWire(0);  // SHTC3 - GPIO22(SDA), GPIO23(SCL)
TwoWire I2C_2 = TwoWire(1);  // BMP180 - GPIO19(SDA), GPIO21(SCL)

// Structure to hold sensor readings
struct SensorData {
  float temperature;
  float humidity;
  float pressure;
  bool valid;
};

// Function prototypes
SensorData readSHTC3();
SensorData readBMP180();
SensorData readBME280();
void printSensorComparison(SensorData shtc3_data, SensorData bmp180_data, SensorData bme280_data);
void printSeparator();

void setup() {
  Serial.begin(115200);
  delay(1000);
  
  Serial.println("ESP32 Multi-Sensor Temperature Calibrator");
  Serial.println("==========================================");
  
  // Initialize I2C buses - using boot-safe pins (BME280 disabled for now)
  I2C_1.begin(22, 21);  // SHTC3: SDA=GPIO22, SCL=GPIO21
  I2C_2.begin(16, 17);   // BME280: SDA=GPIO16, SCL=GPIO17  
  Serial.println("Initializing sensors...");
  
  // Initialize SHTC3
  if (shtc3.begin(&I2C_1)) {
    Serial.println("✓ SHTC3 initialized successfully");
    shtc3_available = true;
  } else {
    Serial.println("✗ Failed to initialize SHTC3");
  }
  
  // Initialize BMP180 (mode first, then wire)
  if (bmp180.begin(BMP085_ULTRAHIGHRES, &I2C_1)) {
    Serial.println("✓ BMP180 initialized successfully");
    bmp180_available = true;
  } else {
    Serial.println("✗ Failed to initialize BMP180");
  }

  // Initialize BME280
  if (bme280.begin(0x76, &I2C_2)) {
    Serial.println("✓ BME280 initialized successfully");
    bme280_available = true;
  } else {
    Serial.println("✗ Failed to initialize BME280");
  }
  
  printSeparator();
  Serial.println("Starting measurements with available sensors...");
  printSeparator();
}

void loop() {
  // Read data from available sensors only
  SensorData shtc3_data = {0, 0, 0, false};
  SensorData bmp180_data = {0, 0, 0, false};
  SensorData bme280_data = {0, 0, 0, false};
  
  if (shtc3_available) {
    shtc3_data = readSHTC3();
  }
  
  if (bmp180_available) {
    bmp180_data = readBMP180();
  }
  
  if (bme280_available) {
    bme280_data = readBME280();
  }
  
  // Calculate and display statistics only for working sensors
  float temps[3];
  int valid_temp_count = 0;
  
  if (shtc3_data.valid) temps[valid_temp_count++] = shtc3_data.temperature;
  if (bmp180_data.valid) temps[valid_temp_count++] = bmp180_data.temperature;
  if (bme280_data.valid) temps[valid_temp_count++] = bme280_data.temperature;
  
  if (valid_temp_count > 1) {
    // Calculate temperature statistics
    float temp_sum = 0;
    float temp_min = temps[0];
    float temp_max = temps[0];
    
    for (int i = 0; i < valid_temp_count; i++) {
      temp_sum += temps[i];
      if (temps[i] < temp_min) temp_min = temps[i];
      if (temps[i] > temp_max) temp_max = temps[i];
    }
    
    float temp_avg = temp_sum / valid_temp_count;
    float temp_range = temp_max - temp_min;
      // Print header for data table
    Serial.println("┌─────────┬───────────────┬──────────┬───────────┬────────┐");
    Serial.println("│ Sensor  │ Temperature   │ Humidity │ Pressure  │ Status │");
    Serial.println("├─────────┼───────────────┼──────────┼───────────┼────────┤");
      
    // Print comparison table
    printSensorComparison(shtc3_data, bmp180_data, bme280_data);

    Serial.println("├─────────┼───────────────┼──────────┼───────────┼────────┤");
    Serial.printf ("│ STATS   │ Avg: %6.2f°C │          │           │        │\n", temp_avg);
    Serial.printf ("│         │ Range:%5.2f°C │          │           │        │\n", temp_range);
    Serial.printf ("│         │ Min: %6.2f°C │          │           │        │\n", temp_min);
    Serial.printf ("│         │ Max: %6.2f°C │          │           │        │\n", temp_max);
  } else if (valid_temp_count == 1) {
    Serial.println("├─────────┼───────────────┼──────────┼───────────┼────────┤");
    Serial.printf ("│ STATS   │ Only 1 sensor │          │           │        │\n");
  } else {
    Serial.println("├─────────┼───────────────┼──────────┼───────────┼────────┤");
    Serial.printf ("│ STATS   │ No valid data │          │           │        │\n");
  }

    Serial.println("└─────────┴───────────────┴──────────┴───────────┴────────┘");
    Serial.println();
  
  delay(5000);  // Wait 5 seconds between readings
}

void printSensorComparison(SensorData shtc3_data, SensorData bmp180_data, SensorData bme280_data) {
  // SHTC3 row
    Serial.printf ("│ SHTC3   │   %9.2f°C │  %6.2f%% │     N/A   │ %s │\n", 
                shtc3_data.valid ? shtc3_data.temperature : 0.0,
                shtc3_data.valid ? shtc3_data.humidity : 0.0,
                shtc3_data.valid ? "  OK  " : " FAIL ");
  
  // BMP180 row
    Serial.printf ("│ BMP180  │   %9.2f°C │    N/A   │%7.1fhPa │ %s │\n",
                bmp180_data.valid ? bmp180_data.temperature : 0.0,
                bmp180_data.valid ? bmp180_data.pressure : 0.0,
                bmp180_data.valid ? "  OK  " : " FAIL ");
  
  // BME280 row - show as disabled
    Serial.printf ("│ BME280  │     DISABLED  │    N/A   │     N/A   │  SKIP  │\n");
}

SensorData readSHTC3() {
  SensorData data = {0, 0, 0, false};
  
  if (!shtc3_available) return data;
  
  sensors_event_t humidity, temp;
  if (shtc3.getEvent(&humidity, &temp)) {
    data.temperature = temp.temperature;
    data.humidity = humidity.relative_humidity;
    data.pressure = 0;  // SHTC3 doesn't measure pressure
    data.valid = true;
  }
  
  return data;
}

SensorData readBMP180() {
  SensorData data = {0, 0, 0, false};
  
  if (!bmp180_available) return data;
  
  float temp = bmp180.readTemperature();
  float pressure = bmp180.readPressure() / 100.0;  // Convert Pa to hPa
  
  // Check if readings are valid (BMP180 returns specific values for errors)
  if (!isnan(temp) && !isnan(pressure) && temp > -40 && temp < 85) {
    data.temperature = temp;
    data.humidity = 0;  // BMP180 doesn't measure humidity
    data.pressure = pressure;
    data.valid = true;
  }
  
  return data;
}

SensorData readBME280() {
  SensorData data = {0, 0, 0, false};
  
  if (!bme280_available) return data;
  
  try {
    float temp = bme280.readTemperature();
    float humidity = bme280.readHumidity();
    float pressure = bme280.readPressure() / 100.0;  // Convert Pa to hPa
    
    // Check if readings are valid
    if (!isnan(temp) && !isnan(humidity) && !isnan(pressure)) {
      data.temperature = temp;
      data.humidity = humidity;
      data.pressure = pressure;
      data.valid = true;
    }
  } catch (...) {
    // BME280 reading failed, return invalid data
  }
  
  return data;
}

void printSeparator() {
  Serial.println("==========================================");
}