#include <Arduino.h>
#include <SPI.h>
#include <Adafruit_GFX.h>
#include <Adafruit_ST7789.h>
#include "Display_ST7789.h"  // Only for pin definitions
#include "Fonts/FreeSans24pt7b.h"

#define PIN_NEOPIXEL 38

// Define color constants
#define BLACK   0x0000
#define GREEN   0x07E0

// Flag to control when to update the display
bool displayInitialized = false;

// Create an instance of the ST7789 display
//Adafruit_ST7789 display = Adafruit_ST7789(EXAMPLE_PIN_NUM_LCD_CS, EXAMPLE_PIN_NUM_LCD_DC, EXAMPLE_PIN_NUM_LCD_RST);
Adafruit_ST7789 display = Adafruit_ST7789(EXAMPLE_PIN_NUM_LCD_CS, EXAMPLE_PIN_NUM_LCD_DC, EXAMPLE_PIN_NUM_MOSI, EXAMPLE_PIN_NUM_SCLK,  EXAMPLE_PIN_NUM_LCD_RST);

// The text to display - this can be updated to show actual temperature
const char* temperatureText = "24.5°C";

void setup() {
  Serial.begin(115200);
  Serial.println("Temperature Display Init");
  
  // Make sure the RGB LED is initialized first
  pinMode(PIN_NEOPIXEL, OUTPUT);

  Serial.println("Initializing display now...");
    
  // Initialize the display using the Adafruit library
  display.init(LCD_WIDTH, LCD_HEIGHT);  // Native 172x320 resolution
  
  // Set the rotation to 1 for landscape mode (90 degrees)
  // 0 = Portrait, 1 = Landscape, 2 = Inverted Portrait, 3 = Inverted Landscape
  display.setRotation(1);
  
  // Set backlight
  Backlight_Init();
  Set_Backlight(100); // Set backlight to maximum
  
  // Fill screen with black
  display.fillScreen(GREEN);
  
  // Set text color to green
  display.setTextColor(BLACK);
}

void loop() {
  // First cycle of LED colors to make sure it's working
  rgbLedWrite(PIN_NEOPIXEL, 0, 255, 0); // Green
  delay(1000);
  rgbLedWrite(PIN_NEOPIXEL, 0, 0, 255); // Blue
  delay(1000);
  rgbLedWrite(PIN_NEOPIXEL, 255, 0, 0); // Red
  delay(1000);

  // Calculate the center position for the text
  int16_t x1, y1;
  uint16_t w, h;
  display.setFont(&FreeSans24pt7b);
  display.getTextBounds(temperatureText, 0, 0, &x1, &y1, &w, &h);
  
  int x = (display.width() - w) / 2;
  int y = (display.height() - h) / 2 + h; // Add h to account for text baseline
  
  display.setCursor(x, y);
  display.print(temperatureText);
  
  Serial.println("Display initialized and text drawn");
}
