#include "LEDLib.h"

RGBLED::RGBLED(int redPin, int greenPin, int bluePin) {
    pinRed = redPin;
    pinGreen = greenPin;
    pinBlue = bluePin;
    
    pinMode(pinRed, OUTPUT);
    pinMode(pinGreen, OUTPUT);
    pinMode(pinBlue, OUTPUT);
    
    turnOff(); // Initialize LED as off
}

void RGBLED::turnOn() {
    turnOn(defaultColor, defaultBrightness);
}

void RGBLED::turnOn(Color color) {
    turnOn(color, defaultBrightness);
}

void RGBLED::turnOn(Color color, int brightness) {
    setColor(color, brightness);
}

void RGBLED::turnOff() {
    analogWrite(pinRed, 0);
    analogWrite(pinGreen, 0);
    analogWrite(pinBlue, 0);
}

void RGBLED::blink(Color color, int waitTime, int repetitions, int brightness) {
    for (int i = 0; i < repetitions; i++) {
        turnOn(color, brightness);
        delay(waitTime);
        turnOff();
        delay(waitTime);
    }
}

void RGBLED::setColor(Color color) {
    setColor(color, defaultBrightness);
}

void RGBLED::setColor(Color color, int brightness) {
    // Apply brightness scaling to each color component
    int red = (color.r * brightness) / 255;
    int green = (color.g * brightness) / 255;
    int blue = (color.b * brightness) / 255;
    
    setRGB(red, green, blue);
}

void RGBLED::setRGB(int red, int green, int blue) {
    // Constrain values to 0-255 range
    red = constrain(red, 0, 255);
    green = constrain(green, 0, 255);
    blue = constrain(blue, 0, 255);
    
    analogWrite(pinRed, red);
    analogWrite(pinGreen, green);
    analogWrite(pinBlue, blue);
}

void RGBLED::setBrightness(int brightness) {
    // This method applies brightness to the current color
    // For simplicity, we'll use the default color
    setColor(defaultColor, brightness);
}
