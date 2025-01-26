#include <Arduino.h>
#include <SoftwareSerial.h>

const int ledPin = 19; // Define the pin for the internal LED
const int irPin = 23;   // Define the pin for the IR sensor

EspSoftwareSerial::UART serialPort;

// // Becomes set from ISR / IRQ callback function.
// std::atomic<bool> rxPending(false);

// void IRAM_ATTR receiveHandler() {
// 	rxPending.store(true);
// 	esp_schedule();
// }

void setup() {
    pinMode(ledPin, OUTPUT); // Initialize the LED pin as an output
    pinMode(irPin, INPUT);   // Initialize the IR pin as an input
    Serial.begin(115200);    // Start the Serial communication at 115200 baud rate
    Serial.println("IR Sensor Test"); // Print the message on the Serial Monitor

    serialPort.begin(9600, SWSERIAL_8N1, irPin, ledPin, false);
    if (!serialPort) { // If the object did not initialize, then its configuration is invalid
        Serial.println("Invalid EspSoftwareSerial pin configuration, check config"); 
        while (1) { // Don't continue with invalid configuration
            delay (1000);
        }
    } 
    //serialPort.onReceive(receiveHandler);
}

void loop() {
    digitalWrite(ledPin, LOW); // Turn the LED on
    // delay(1000);                // Wait for a second
    // digitalWrite(ledPin, LOW);  // Turn the LED off
    // delay(10);   
    
    //Read IR data
    // uint8_t irData = 0;
    // for (int i = 0; i < 8; i++) {
    //     unsigned long duration = pulseIn(irPin, HIGH);
    //     if (duration > 1000) { // Adjust threshold as needed
    //         irData |= (1 << i);
    //     }
    // }

    // // Print the byte stream to the Serial Monitor
    // Serial.print("IR Data: ");
    // Serial.println(irData, BIN); // Print as binary

    //Serial.print(digitalRead(irPin));

    while (serialPort.available() > 0) {
        //Serial.print("0x");
        Serial.print(serialPort.read(), HEX);
        Serial.print(" ");
        yield();
    }

}