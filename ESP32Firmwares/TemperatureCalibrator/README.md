# ESP32 Temperature Calibrator

This project reads temperature, humidity, and pressure data from multiple sensors connected to an ESP32 to help calibrate and compare sensor accuracy.

## Hardware Setup

### Sensors and GPIO Connections

| Sensor | Type | SDA Pin | SCL Pin | Measures |
|--------|------|---------|---------|----------|
| SHTC3 | Temperature/Humidity | GPIO22 | GPIO21 | Temperature, Humidity |
| BMP180 | Pressure/Temperature | GPIO19 | GPIO18 | Temperature, Pressure |
| BME280 | All-in-one | GPIO16 | GPIO17 | Temperature, Humidity, Pressure |

### Wiring Diagram

```
ESP32           SHTC3           BMP180          BME280
-----           -----           ------          ------
GPIO22  <-----> SDA
GPIO21  <-----> SCL
GPIO19  <-----> | |             SDA
GPIO18  <-----> | |             SCL
GPIO16  <-----> | |             | |             SDA
GPIO17  <-----> | |             | |             SCL
GND     <-----> GND             GND             GND
3.3V    <-----> VCC             VCC             VCC
```

### ⚠️ Important: Boot-Safe GPIO Selection

The ESP32 has several "strapping pins" that are checked during boot and can prevent the device from starting or uploading firmware if they're pulled to the wrong logic level by connected sensors:

**Problematic pins to AVOID for I2C:**
- GPIO0, GPIO2, GPIO5, GPIO12, GPIO15 - Boot strapping pins
- GPIO1, GPIO3 - Serial communication (TX/RX)
- GPIO6-11 - Connected to internal flash

**Safe pins for I2C (used in this project):**
- GPIO16, GPIO17, GPIO18, GPIO19, GPIO21, GPIO22 - General purpose, boot-safe

## Features

- **Multi-I2C Support**: Each sensor uses its own I2C bus to prevent conflicts
- **Real-time Comparison**: Shows all sensor readings in a formatted table
- **Statistical Analysis**: Calculates temperature averages, ranges, and outliers
- **Error Handling**: Displays sensor status and handles initialization failures
- **Clean Output**: Easy-to-read table format for comparing sensor values

## Sample Output

```
ESP32 Multi-Sensor Temperature Calibrator
==========================================
Initializing sensors...
✓ SHTC3 initialized successfully
✓ BMP180 initialized successfully
✓ BME280 initialized successfully at address 0x76
==========================================
Starting measurements...
==========================================
Sensor Comparison Table:
│ Sensor  │ Temperature │ Humidity │ Pressure  │ Status │
├─────────┼─────────────┼──────────┼───────────┼────────┤
│ SHTC3   │    23.45°C  │  45.20%  │     N/A   │   OK   │
│ BMP180  │    23.52°C  │    N/A   │ 1013.2hPa │   OK   │
│ BME280  │    23.48°C  │  45.18%  │ 1013.1hPa │   OK   │
├─────────┼─────────────┼──────────┼───────────┼────────┤
│ STATS   │ Avg: 23.48°C│          │           │        │
│         │ Range: 0.07°C│          │           │        │
│         │ Min: 23.45°C │          │           │        │
│         │ Max: 23.52°C │          │           │        │
└─────────┴─────────────┴──────────┴───────────┴────────┘
```

## Usage

1. **Build and Upload**:
   ```bash
   pio run --target upload
   ```

2. **Monitor Serial Output**:
   ```bash
   pio device monitor
   ```
   Or open Serial Monitor at 115200 baud in your IDE

3. **Analyze Results**:
   - Compare temperature readings across all sensors
   - Look for consistent outliers to identify less accurate sensors
   - Use the statistical data to determine sensor precision
   - Note any sensors showing "FAIL" status

## Configuration

### Measurement Interval
Default: 5 seconds between readings
To change: Modify `delay(5000)` in the main loop

### I2C Addresses
- BME280: Automatically tries 0x76 and 0x77
- BMP180: Fixed address (auto-detected)
- SHTC3: Fixed address (auto-detected)

### Libraries Used
- Adafruit SHTC3 Library
- Adafruit BMP085 Library (for BMP180)
- Adafruit BME280 Library
- Adafruit BusIO
- Adafruit Unified Sensor

## Troubleshooting

### Sensor Not Detected
1. Check wiring connections
2. Verify power supply (3.3V)
3. Ensure proper I2C pull-up resistors (usually built into modules)
4. Try different I2C addresses for BME280 (0x76 or 0x77)

### Inconsistent Readings
- Allow sensors to stabilize (first few readings may vary)
- Ensure sensors are at the same temperature
- Check for interference from other devices

### Build Errors
- Ensure all libraries are properly installed via PlatformIO
- Check that GPIO pins don't conflict with other uses

## Files

- `src/main.cpp` - Main application code
- `include/sensor_config.h` - Configuration constants
- `platformio.ini` - PlatformIO project configuration

## License

This project is open source and available under standard terms.