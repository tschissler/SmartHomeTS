#include "LEDLib.h"

LED::LED(int ledPin) {
    pin = ledPin;
    pinMode(pin, OUTPUT);
    turnOff(); // Initialize LED as off
}

void LED::turnOn() {
    digitalWrite(pin, HIGH);
}

void LED::turnOn(int brightness) {
    if (brightness <= 0) {
        turnOff();
        return;
    }
    
    if (brightness >= 255) {
        digitalWrite(pin, HIGH);
    } else {
        analogWrite(pin, brightness);
    }
}

void LED::turnOff() {
    digitalWrite(pin, LOW);
}

void LED::blink(int waitTime, int repetitions, int brightness) {
    for (int i = 0; i < repetitions; i++) {
        turnOn(brightness);
        delay(waitTime);
        turnOff();
        
        // Don't delay after the last blink
        if (i < repetitions - 1) {
            delay(waitTime);
        }
    }
}

void LED::setBrightness(int brightness) {
    if (brightness <= 0) {
        turnOff();
    } else if (brightness >= 255) {
        digitalWrite(pin, HIGH);
    } else {
        analogWrite(pin, brightness);
    }
}