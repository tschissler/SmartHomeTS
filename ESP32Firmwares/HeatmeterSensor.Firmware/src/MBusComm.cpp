#include "MBusComm.h"
#include "MBusParser.h"  // Include to access the shared debug variable

// Constructor
MBusComm::MBusComm() : rxPin(-1), txPin(-1), baud(2400) {
}

bool MBusComm::init(int rxPin, int txPin, long baud, bool debugMode) {
    this->rxPin = rxPin;
    this->txPin = txPin;
    this->baud = baud;
    debug = debugMode;
    Serial1.begin(this->baud, SERIAL_8E1, this->rxPin, this->txPin);
    return true;
}

// Send 504 bytes 0x55 with 8N1, then switch back to 8E1.
// Requirement per WingStar/Ultramess documentation: before EVERY telegram (preamble).
void MBusComm::sendPreamble() {
    // briefly switch to 8N1
    Serial1.end();
    Serial1.begin(this->baud, SERIAL_8N1, this->rxPin, this->txPin);

    if (debug) Serial.println("Sending preamble 504 bytes 0x55 with 8N1...");

    uint8_t pre = 0x55;
    for (int i = 0; i < 504; ++i) {
        Serial1.write(pre);
    }
    Serial1.flush();

    // immediately back to 8E1 for M-Bus
    Serial1.end();
    Serial1.begin(this->baud, SERIAL_8E1, this->rxPin, this->txPin);

    delay(100);
}

// Send SND_NKE (wake-up): 0x10 0x40 A CS 0x16, preceded by preamble.
// Expected response: 0xE5 (Ack).
bool MBusComm::sendWakeUp(uint8_t primaryAddress) {
    sendPreamble();  

    uint8_t C = 0x40;
    uint8_t A = primaryAddress;
    uint8_t checksum = (C + A) & 0xFF;
    uint8_t frame[5] = { 0x10, C, A, checksum, 0x16 };
    if (debug) {
    Serial.print("Sending wake-up ... ");
        for (int i = 0; i < 5; ++i) {
            Serial.print("0x");
            Serial.print(frame[i], HEX);
            Serial.print(" ");
        }
        Serial.println();
        Serial.println();
    }

    Serial1.write(frame, 5);
    Serial1.flush();

    return waitForAck(frame);
}

bool MBusComm::waitForAck(uint8_t frame[5])
{
    if (debug)
        Serial.println("Waiting for ack...");
    unsigned long start = millis();
    while (millis() - start < 1000)
    {
        if (Serial1.available())
        {
            uint8_t read = Serial1.read();
            if (read == 0xE5)
            {
                if (debug)
                    Serial.println("Ack received.");
                return true;
            }

            // Prüfen, ob das gelesene Byte zuvor gesendet wurde, Echo ignorieren
            if (std::find(frame, frame + 5, read) != frame + 5)
            {
                if (debug)
                {
                    Serial.print("Echo received, ignoring: 0x");
                    Serial.println(read, HEX);
                }
                continue;
            }

            Serial.print("Unexpected byte received: 0x");
            Serial.println(read, HEX);
        }
    }
    return false;
}

// Send REQ_UD2 (data request): 0x10 0x5B/0x7B A CS 0x16, preceded by preamble.
// Response: long frame (0x68 ... 0x16) with CI=0x72.
bool MBusComm::sendRequest(uint8_t primaryAddress) {
    sendPreamble(); 

    if (debug) Serial.println("Sending request ... ");
    uint8_t C = 0x5B; 
    uint8_t A = primaryAddress;
    uint8_t checksum = (C + A) & 0xFF;
    uint8_t frame[5] = { 0x10, C, A, checksum, 0x16 };
    Serial1.write(frame, 5);
    Serial1.flush();
    return true;
}

int MBusComm::readResponse(uint8_t *buffer, int maxLen) {
    // Auf 0x68 warten
    unsigned long startTime = millis();
    int idx = 0;
    if (debug) Serial.println("Waiting for long frame identifier:");
    while (millis() - startTime < 1500) {
        if (Serial1.available()) {
            uint8_t b = Serial1.read();
            if (debug) {
                Serial.print(" 0x");
                Serial.print(b, HEX);
            }
            if (b == 0x68) { buffer[idx++] = b; break; }
        }
    }
    if (idx == 0) return -1;

    // Zwei Längen + zweites 0x68
    while (idx < 4 && millis() - startTime < 2000) {
        if (Serial1.available()) buffer[idx++] = Serial1.read();
    }
    if (idx < 4 || buffer[3] != 0x68) return -2;

    uint8_t len1 = buffer[1], len2 = buffer[2];
    if (len1 != len2) return -3;

    int toRead = len1 + 2; // C,A,CI + userdata + csum + 0x16
    while (idx < 4 + toRead && millis() - startTime < 3500) {
        if (Serial1.available()) buffer[idx++] = Serial1.read();
    }
    if (idx < 4 + toRead) return -4;

    int total = idx;
    if (buffer[total - 1] != 0x16) return -5;

    uint8_t cs = 0;
    for (int i = 4; i < total - 2; ++i) cs += buffer[i];
    if (cs != buffer[total - 2]) return -6;

    if (debug) {
        Serial.println();
    Serial.println("Received complete frame:");
        for (int i = 0; i < total; ++i) {
            Serial.print("0x");
            Serial.print(buffer[i], HEX);
            Serial.print(" ");
        }
        Serial.println();
        Serial.println();
    }
    return total;
}


