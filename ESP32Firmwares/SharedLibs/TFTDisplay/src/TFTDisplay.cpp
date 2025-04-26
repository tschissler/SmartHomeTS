#include "TFTDisplay.h"

TFTDisplay::TFTDisplay() : tft(TFT_CS, TFT_DC, TFT_MOSI, TFT_SCLK, TFT_RST) {}

void TFTDisplay::init() {
    tft.initR(INITR_BLACKTAB);      // Initialize a ST7735S chip, black tab
    tft.fillScreen(ST77XX_BLACK);   // Fill screen with black color
    tft.setRotation(1);             // Set orientation (0-3)

    // Text settings
    tft.setTextSize(1);             // Set text size
    tft.setTextColor(ST77XX_WHITE); // Set text color
}

void TFTDisplay::printInformation(const DisplayInformation& data) {
    // Clear TFT screen
    tft.fillScreen(ST77XX_BLACK);
    tft.setCursor(0, 0);

    tft.println("TemperatureSensor");
    tft.print("Version: ");
    tft.println(data.version);
    tft.print("Chip ID: ");
    tft.println(data.chipID);
    tft.print("Sensor Name: ");
    tft.println(data.sensorName);
    tft.println();
    tft.println(data.time);
    tft.println("Temperature: " + data.temperature + "C");
    tft.println("Humidity: " + data.humidity + "%");
    tft.println("IP: " + data.ip);
    tft.println("SSID: " + data.ssid);
    tft.println("RSSI: " + data.rssi + " dBm");

    if (data.displayMQTTMessage) {
        tft.println();
        if (data.mqttEnabled) {
            tft.println(data.mqttSuccess?"MQTT sent successfully":"Sending MQTT failed");
        } else {
            tft.println("MQTT messages disabled");
        }
    }

    tft.println(data.pingSuccess?"Ping successful":"Ping failed");
}