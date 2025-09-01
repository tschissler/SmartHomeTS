#include <Arduino.h>
#include "MBusComm.h"
#include "MBusParser.h"

const int IR_RX_PIN = D5;            // IR phototransistor input
const int IR_TX_PIN = D8;            // IR LED output
const uint8_t METER_ADDRESS = 0xFE;     // M-Bus primary address (0 = default)
const unsigned long READ_INTERVAL_MS = 10 * 60 * 1000;  // Read interval (e.g. 10 minutes)
unsigned long lastReadTime = -10000000;

void setup() {
  Serial.begin(115200);
  while (!Serial) { }
  Serial.println("Initializing M-Bus interface...");
  if (!MBusInit(IR_RX_PIN, IR_TX_PIN, 2400, true)) {
      Serial.println("Error initializing M-Bus interface.");
  } else {
      Serial.println("M-Bus interface ready.");
  }
}

void loop() {
  if (millis() - lastReadTime >= READ_INTERVAL_MS) {
    lastReadTime = millis();
    Serial.println();
    Serial.println("Start reading meter data...");
    // Sending signal to wake-up meter and waiting for acknowledging
    bool ack = MBusSendWakeUp(METER_ADDRESS);

    if (!ack) {
      Serial.println("Meter did not respond to wake-up. Please check connection and/or address.");
      return;
    }
    // Sending request and reading response
    MBusSendRequest(METER_ADDRESS);
    uint8_t frameBuf[300];
    int frameLen = MBusReadResponse(frameBuf, sizeof(frameBuf));
    if (frameLen < 0) {
      Serial.print("Error reading frame, code ");
      Serial.println(frameLen);
    } else {
      // Parsing frame and data output
      parseMBusFrame(frameBuf, frameLen);
    }
  }
}
