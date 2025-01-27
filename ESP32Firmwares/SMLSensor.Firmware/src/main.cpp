#include <Arduino.h>
#include <SoftwareSerial.h>
#include <sml/sml_file.h>
#include <deque>
#include <SMLParser.h>

const int ledPin = 19; // Define the pin for the internal LED
const int irPin = 23;   // Define the pin for the IR sensor

EspSoftwareSerial::UART serialPort;

#define BUFFER_SIZE 4096

std::deque<uint8_t> buffer;
const std::vector<uint8_t> startSequence = {0x1B, 0x1B, 0x1B, 0x1B, 0x01, 0x01, 0x01, 0x01};
const std::vector<uint8_t> endSequencePrefix = {0x1B, 0x1B, 0x1B, 0x1B, 0x1A};
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

    // Read data from the serial port
    while (serialPort.available() > 0) {
        uint8_t data = serialPort.read();
        if (buffer.size() >= BUFFER_SIZE) {
            buffer.pop_front();  // Remove the oldest data
        }
        buffer.push_back(data);
        yield();
    }

    // Check if the buffer contains a complete burst
    if (buffer.size() >= startSequence.size() + endSequencePrefix.size() + 3) {
        bool startFound = false;
        bool endFound = false;
        int startIndex = -1;
        int endIndex = -1;

        // Find the start sequence
        for (size_t i = 0; i <= buffer.size() - startSequence.size(); ++i) {
            bool match = true;
            for (size_t j = 0; j < startSequence.size(); ++j) {
                if (buffer[i + j] != startSequence[j]) {
                    match = false;
                    break;
                }
            }
            if (match) {
                startIndex = i + startSequence.size();
                startFound = true;
                break;
            }
        }

        // Find the end sequence
        if (startFound) {
            for (size_t i = startIndex; i <= buffer.size() - endSequencePrefix.size() - 3; ++i) {
                bool match = true;
                for (size_t j = 0; j < endSequencePrefix.size(); ++j) {
                    if (buffer[i + j] != endSequencePrefix[j]) {
                        match = false;
                        break;
                    }
                }
                if (match) {
                    endIndex = i;
                    endFound = true;
                    break;
                }
            }
        }

        // If both start and end sequences are found, parse the data
        if (startFound && endFound) {
            std::vector<uint8_t> bufferVector(buffer.begin() + startIndex, buffer.begin() + endIndex);
            try {
                // Call the Parse method
                SMLData* smlData = SMLParser::Parse(bufferVector);

                // Check if the parsing was successful
                if (smlData) {
                    // Output the parsed data
                    Serial.print("Tarif1: ");
                    Serial.println(smlData->Tarif1);
                    Serial.print("Tarif2: ");
                    Serial.println(smlData->Tarif2);
                    Serial.print("Power: ");
                    Serial.println(smlData->Power);

                    // Clean up the allocated memory
                    delete smlData;
                } else {
                    Serial.println("Parsing failed: No data returned");
                }
            } catch (const std::exception& ex) {
                // Handle any exceptions that occur during parsing
                Serial.print("Exception occurred: ");
                Serial.println(ex.what());
            }

            // Remove the processed data from the buffer
            buffer.erase(buffer.begin(), buffer.begin() + endIndex + endSequencePrefix.size() + 3);
        }
    }
}