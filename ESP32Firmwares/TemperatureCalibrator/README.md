# ESP32 Temperature Calibrator

This project reads temperature, humidity, and pressure data from multiple sensors connected to an ESP32 to help calibrate and compare sensor accuracy.

## Hardware Setup

### Sensors and GPIO Connections

| Sensor | Type | SDA Pin | SCL Pin | I2C Bus | Measures |
|--------|------|---------|---------|---------|----------|
| SHTC3 | Temperature/Humidity | GPIO22 | GPIO21 | I2C_1 | Temperature, Humidity |
| BMP180 | Pressure/Temperature | GPIO22 | GPIO21 | I2C_1 | Temperature, Pressure |
| BME280 | All-in-one | GPIO16 | GPIO17 | I2C_2 | Temperature, Humidity, Pressure |

### Wiring Diagram

```
ESP32           SHTC3/BMP180    BME280
-----           ------------    ------
GPIO22  <-----> SDA (shared)
GPIO21  <-----> SCL (shared)
GPIO16  <-----> | |             SDA
GPIO17  <-----> | |             SCL
GND     <-----> GND             GND
3.3V    <-----> VCC             VCC
```

### ⚠️ Important: I2C Bus Configuration

The ESP32 supports only 2 hardware I2C buses, so this project uses:

**I2C Bus 1 (GPIO22/21):**
- SHTC3 and BMP180 share the same I2C bus
- Both sensors have different I2C addresses, so no conflicts

**I2C Bus 2 (GPIO16/17):**
- BME280 uses a separate I2C bus

**Boot-Safe GPIO Selection:**

The following pins are avoided as they can interfere with ESP32 boot/upload:
- GPIO0, GPIO2, GPIO5, GPIO12, GPIO15 - Boot strapping pins
- GPIO1, GPIO3 - Serial communication (TX/RX)
- GPIO6-11 - Connected to internal flash

**Safe pins used in this project:**
- GPIO16, GPIO17, GPIO21, GPIO22 - General purpose, boot-safe

## Features

- **Dual I2C Bus Support**: Uses ESP32's 2 hardware I2C buses to handle 3 sensors efficiently
- **Shared I2C Bus**: SHTC3 and BMP180 share I2C_1, BME280 uses I2C_2
- **Real-time Comparison**: Shows all sensor readings side-by-side every 5 seconds
- **Comprehensive Statistics**: Calculates average, range, min, and max temperatures across all working sensors
- **Error Handling**: Shows sensor status (OK/FAIL) and handles sensor initialization failures
- **Boot-Safe Design**: Uses GPIO pins that don't interfere with ESP32 boot process
- **Clean Output Format**: Professional table format for easy sensor comparison

## Sample Output

```
ESP32 Multi-Sensor Temperature Calibrator
==========================================
Initializing sensors...
✓ SHTC3 initialized successfully
✓ BMP180 initialized successfully
✓ BME280 initialized successfully
==========================================
Starting measurements with available sensors...
==========================================
┌─────────┬───────────────┬──────────┬───────────┬────────┐
│ Sensor  │ Temperature   │ Humidity │ Pressure  │ Status │
├─────────┼───────────────┼──────────┼───────────┼────────┤
│ SHTC3   │     23.45°C   │  45.20%  │     N/A   │   OK   │
│ BMP180  │     23.52°C   │    N/A   │ 1013.2hPa │   OK   │
│ BME280  │     23.48°C   │  45.18%  │ 1013.1hPa │   OK   │
├─────────┼───────────────┼──────────┼───────────┼────────┤
│ STATS   │ Avg: 23.48°C  │          │           │        │
│         │ Range: 0.07°C │          │           │        │
│         │ Min: 23.45°C  │          │           │        │
│         │ Max: 23.52°C  │          │           │        │
└─────────┴───────────────┴──────────┴───────────┴────────┘
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
   - Compare temperature readings across all three sensors
   - SHTC3 and BME280 provide humidity comparison
   - BMP180 and BME280 provide pressure comparison  
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

### I2C Bus Assignment
- **I2C_1 (GPIO22/21)**: SHTC3 + BMP180 (shared bus)
- **I2C_2 (GPIO16/17)**: BME280 (dedicated bus)

### Libraries Used
- Adafruit SHTC3 Library
- Adafruit BMP085 Library (for BMP180)
- Adafruit BME280 Library
- Adafruit BusIO
- Adafruit Unified Sensor

## Troubleshooting

### Sensor Not Detected
1. Check wiring connections (especially shared I2C bus connections)
2. Verify power supply (3.3V)
3. Ensure proper I2C pull-up resistors (usually built into modules)
4. Try different I2C addresses for BME280 (0x76 or 0x77)
5. Check that SHTC3 and BMP180 aren't conflicting on shared bus

### Inconsistent Readings
- Allow sensors to stabilize (first few readings may vary)
- Ensure all sensors are at the same ambient temperature
- Check for interference from other devices
- Verify no loose connections on shared I2C bus

### Build Errors
- Ensure all libraries are properly installed via PlatformIO
- Check that GPIO pins don't conflict with other uses

## Files

- `src/main.cpp` - Main application code
- `include/sensor_config.h` - Configuration constants
- `platformio.ini` - PlatformIO project configuration

## License

This project is open source and available under standard terms.