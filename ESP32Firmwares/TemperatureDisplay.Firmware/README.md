# Temperature Display Firmware (ESP32‑S3)

A modern, multi-room temperature and thermostat display for ESP32‑S3 with a smooth touch UI powered by LVGL. Built to be reliable on large RGB LCD panels and easy to integrate into a smart home.

The visual UI was designed and generated with SquareLine Studio ([squareline.io](https://squareline.io/)) and then integrated with LVGL.

## What this project is

- A firmware that turns an ESP32‑S3 + 7" touch LCD into a responsive temperature/thermostat display.
- Uses a polished LVGL UI (fonts, icons, and themes included) for great readability from across the room.
- Ready for smart‑home integration via MQTT and time‑sync via NTP.

## Hardware

- Target board: ESP32‑S3 with PSRAM; optimized for 7" WaveShare ESP32‑S3 Touch LCD ([Waveshare wiki](https://www.waveshare.com/wiki/ESP32-S3-Touch-LCD-7)).
- 3D printable case can be found [here](../../3DModels/TemperatureSensor).

## Project layout

- `src/` — main application entry and firmware logic
- `lib/temperature_display/` — core UI/data logic for the temperature display and thermostat data
- `lib/lvgl_port/` — LVGL v9 port and UI glue
- `lib/ui/` — generated UI assets (fonts, images, themes) from SquareLine
- `boards/` — board definitions and a large‑app OTA partition table
- `squareline/` — SquareLine Studio project and exported assets ([squareline.io](https://squareline.io/))
- `platformio.ini` — project configuration and dependencies

## Integrations

- MQTT: Publish readings and subscribe to thermostat commands for easy integration with Home Assistant or any MQTT broker.
- NTP: Keeps device time accurate without manual configuration.
