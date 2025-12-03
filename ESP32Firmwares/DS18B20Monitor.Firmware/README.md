# DS18B20 Monitor Firmware

Firmware for an ESP32-based temperature probe that polls all DS18B20 sensors on the OneWire bus and reports readings over the serial monitor. The project targets the PIOArduino build environment, which wraps the PlatformIO toolchain for Arduino-style development.

- **Sensors:** DS18S20, DS18B20, DS1822
- **Target MCU:** ESP32 (adjust the board in `platformio.ini` as needed)
- **Build system:** PIOArduino (PlatformIO core with Arduino tooling)

## Getting Started

1. Install [PIOArduino](https://github.com/espressif/arduino-esp32/tree/master/tools/PIOArduino) or open the folder with the VS Code PlatformIO extension (PIOArduino bundles the same CLI under the hood).
2. Connect the DS18B20 to the ESP32 (GPIO pin defined in `src/main.cpp`, with the required pull-up resistor on the data line).
3. Build and upload the firmware:

    ```bash
    pio run --target upload
    ```

4. Launch the serial monitor to observe readings:

    ```bash
    pio device monitor
    ```

## Firmware Behavior

- Initializes the OneWire bus and searches for connected sensor devices.
- Configures active devices for 12-bit resolution conversions when supported.
- Initiates temperature conversions and waits for completion.
- Prints the unique 64-bit ROM address alongside the temperature in Celsius for each sensor.

## Example Serial Output

```text
Chip     | Resolution | Address                         | Celsius
----------------------------------------------------------------------------
DS18B20  | 12-bit     | 28-FF-1C-A2-16-04-00-65         |  21.81
DS18B20  | 12-bit     | 28-FF-6B-90-17-05-00-44         |  21.69
DS18B20  | 12-bit     | 28-FF-42-3E-18-06-00-B2         |  21.88
```

## Notes

- Update pin assignments or conversion delays in `src/main.cpp` to match your wiring and sensor count.
- When multiple sensors share the bus, each device is enumerated by its unique ROM address before printing the address and temperature.
