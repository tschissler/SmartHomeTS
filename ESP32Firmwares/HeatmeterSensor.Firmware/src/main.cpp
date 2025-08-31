#include <Arduino.h>
#include "MBusComm.h"
#include "MBusParser.h"

const int IR_RX_PIN = D5;            // IR-Phototransistor Eingang
const int IR_TX_PIN = D8;            // IR-LED Ausgang
const uint8_t METER_ADDRESS = 0xFE;     // M-Bus Primäradresse (0 = Standard)
const unsigned long READ_INTERVAL_MS = 10 * 60 * 1000;  // Leseintervall (z.B. 10 Minuten)
unsigned long lastReadTime = -10000000;

void setup() {
  Serial.begin(115200);
  while (!Serial) { /* Warten bis USB Serial aktiv */ }
  Serial.println("Initialisiere M-Bus Interface...");
  if (!MBusInit(IR_RX_PIN, IR_TX_PIN, 2400, true)) {
      Serial.println("Fehler bei der Initialisierung der M-Bus Schnittstelle.");
  } else {
      Serial.println("M-Bus Interface bereit.");
  }
}

void loop() {
  if (millis() - lastReadTime >= READ_INTERVAL_MS) {
    lastReadTime = millis();
    Serial.println();
    Serial.println("Starte Zählerauslesung...");
    // Wake-Up senden
    bool ack = MBusSendWakeUp(METER_ADDRESS);

    if (!ack) {
      Serial.println("Zähler hat nicht auf Wake-Up geantwortet. Überprüfung der Verbindung/Adresse nötig.");
      return;
    }
    // Request senden und Antwort einlesen
    MBusSendRequest(METER_ADDRESS);
    uint8_t frameBuf[300];
    int frameLen = MBusReadResponse(frameBuf, sizeof(frameBuf));
    if (frameLen < 0) {
      Serial.print("Fehler beim Lesen des Rahmens, Code ");
      Serial.println(frameLen);
    } else {
      // Frame parsen und Daten ausgeben
      parseMBusFrame(frameBuf, frameLen);
    }
    // Baudrate wieder auf 2400 setzen (falls zuvor auf 300 gewechselt)
    MBusInit(IR_RX_PIN, IR_TX_PIN, 2400);
  }
}
