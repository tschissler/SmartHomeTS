#ifndef LEDLIB_H
#define LEDLIB_H

#include <Arduino.h>
#include "colors.h"

// Single Color LED Class
class LED {
private:
    int pin;
    int defaultWaitTime = 500;  // Default blink wait time in milliseconds
    int defaultRepetitions = 1; // Default number of blinks
    int defaultBrightness = 255; // Default brightness (0-255)

public:
    // Constructor
    LED(int ledPin);
    
    // Basic operations
    void turnOn();
    void turnOn(int brightness);
    void turnOff();
    
    // Blink operations with optional parameters
    void blink(int waitTime = 500, int repetitions = 1, int brightness = 255);
    
    // Helper method to set PWM brightness
    void setBrightness(int brightness);
};

// RGB LED Class
class RGBLED {
private:
    int pinRed;
    int pinGreen;
    int pinBlue;
    int defaultWaitTime = 500;  // Default blink wait time in milliseconds
    int defaultRepetitions = 1; // Default number of blinks
    int defaultBrightness = 255; // Default brightness (0-255)
    Color defaultColor = WHITE; // Default color

public:
    // Constructor
    RGBLED(int redPin, int greenPin, int bluePin);
    
    // Basic operations
    void turnOn();
    void turnOn(Color color);
    void turnOn(Color color, int brightness);
    void turnOff();
    
    // Blink operations with optional parameters
    void blink(Color color = WHITE, int waitTime = 500, int repetitions = 1, int brightness = 255);
    
    // Helper methods
    void setColor(Color color);
    void setColor(Color color, int brightness);
    void setRGB(int red, int green, int blue);
    void setBrightness(int brightness);
};

#endif