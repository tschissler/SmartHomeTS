#include <Arduino.h>
#include "waveshare_display.h"

void setup() {
    Serial.begin(115200);
    Serial.println("Initializing LCD display start");
    waveshare_display_init(); // Initialize the RGB LCD
    Serial.println("Initializing LCD display end"); 
}

void loop() {
  delay(1000); // Wait for 1 second
}
