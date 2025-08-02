#include <Arduino.h>
#include <SPI.h>
#include <Adafruit_GFX.h>
#include <Adafruit_ST7789.h>
#include "Display_ST7789.h" // Only for pin definitions
#include <U8g2_for_Adafruit_GFX.h>
#include "DHT.h"

#define PIN_NEOPIXEL 38
#define PIN_DHT22 5
#define DHTTYPE DHT22  

// Define color constants
#define BLACK 0x0000
#define GREEN 0x07E0

// Flag to control when to update the display
bool displayInitialized = false;

// Create an instance of the ST7789 display
// Adafruit_ST7789 display = Adafruit_ST7789(EXAMPLE_PIN_NUM_LCD_CS, EXAMPLE_PIN_NUM_LCD_DC, EXAMPLE_PIN_NUM_LCD_RST);
Adafruit_ST7789 display = Adafruit_ST7789(EXAMPLE_PIN_NUM_LCD_CS, EXAMPLE_PIN_NUM_LCD_DC, EXAMPLE_PIN_NUM_MOSI, EXAMPLE_PIN_NUM_SCLK, EXAMPLE_PIN_NUM_LCD_RST);
U8G2_FOR_ADAFRUIT_GFX u8g2;

// The text to display - this can be updated to show actual temperature
char temperatureText[16] = "24.5°C";
char humidityText[16] = "10.1%";

DHT dht(PIN_DHT22, DHTTYPE);

void setup()
{
  Serial.begin(115200);
  Serial.println("Temperature Display Init");

  // Make sure the RGB LED is initialized first
  pinMode(PIN_NEOPIXEL, OUTPUT);

  Serial.println("Initializing display now...");

  // Initialize the display using the Adafruit library
  display.init(LCD_WIDTH, LCD_HEIGHT); // Native 172x320 resolution
  u8g2.begin(display);

  // Set the rotation to 1 for landscape mode (90 degrees)
  // 0 = Portrait, 1 = Landscape, 2 = Inverted Portrait, 3 = Inverted Landscape
  display.setRotation(1);

  // Set backlight
  Backlight_Init();
  Set_Backlight(100); // Set backlight to maximum

  u8g2.setBackgroundColor(GREEN);
  u8g2.setForegroundColor(BLACK);
  display.fillScreen(GREEN);

  //Init DHT sensor
  dht.begin();
  Serial.println("DHT sensor initialized");
}

void loop()
{
  // First cycle of LED colors to make sure it's working
  // Set a random color for the RGB LED
  int r = random(0, 256);
  int g = random(0, 256);
  int b = random(0, 256);
  rgbLedWrite(PIN_NEOPIXEL, r, g, b);

  float humidity = dht.readHumidity();
  float temperature = dht.readTemperature();
  Serial.println("Humidity: " + String(humidity) + "%, Temperature: " + String(temperature) + "°C");
  snprintf(temperatureText, sizeof(temperatureText), "%.1f°C", temperature); // Safely format temperature string
  snprintf(humidityText, sizeof(humidityText), "%.1f%%", humidity); // Safely format humidity string

  // Calculate the center position for the text
  int16_t x1, y1;
  uint16_t w, h;
  u8g2.setFont(u8g2_font_inr53_mf);
  u8g2.setFontMode(0); // Transparent-Modus (Standard)
  u8g2.setFontDirection(0);
  int16_t ascent = u8g2.getFontAscent();
  int16_t descent = u8g2.getFontDescent();
  h = ascent - descent;
  w = u8g2.getUTF8Width(temperatureText);
  int x = (display.width() - w) / 2;
  int y = (display.height() - h) / 2 + h; // Add h to account for text baseline

  u8g2.drawUTF8(x, y, temperatureText);


  u8g2.setFont(u8g2_font_inb24_mf);
  u8g2.setFontMode(0); // Transparent-Modus (Standard)
  u8g2.setFontDirection(0);
  // int16_t ascent = u8g2.getFontAscent();
  // int16_t descent = u8g2.getFontDescent();
  // h = ascent - descent;
  // w = u8g2.getUTF8Width(temperatureText);
  x = 20;
  y = 30; 

  u8g2.drawUTF8(x, y, humidityText);
  Serial.println("Display initialized and text " + String(temperatureText) + " drawn");
}
