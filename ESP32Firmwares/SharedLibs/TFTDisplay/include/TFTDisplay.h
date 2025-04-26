#ifndef TFTDISPLAY_H
#define TFTDISPLAY_H

#include <Adafruit_GFX.h>
#include <Adafruit_ST7735.h>

#define TFT_CS     15
#define TFT_RST    4  
#define TFT_DC     2
#define TFT_MOSI   23
#define TFT_SCLK   18


struct DisplayInformation {
    String temperature;
    String humidity;
    String version;
    String chipID;
    String sensorName;
    String time;
    String ip;
    String ssid;
    String rssi;
    bool displayMQTTMessage;
    bool mqttEnabled;
    bool mqttSuccess;
    bool pingSuccess;
};

class TFTDisplay {
public:
    TFTDisplay();
    void init();
    void printInformation(const DisplayInformation& data);

private:
    Adafruit_ST7735 tft;
};

#endif // TFTDISPLAY_H