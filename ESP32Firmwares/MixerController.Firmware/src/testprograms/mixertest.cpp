#include <Arduino.h>

// ---------------------------------------------------------------------------
// Mixer commissioning test
//
// Drives the two mixer actuators via single-key commands on the serial
// monitor, so direction wiring and actual mixer behavior can be verified
// on site before deploying the full firmware:
//
//   1 = Mischer_FBHZ auffahren        3 = Mischer_HK auffahren
//   2 = Mischer_FBHZ zufahren         4 = Mischer_HK zufahren
//   0 = alle Antriebe sofort stoppen
//   h = Hilfe anzeigen
//
// Wiring per mixer:
//   direction relay de-energized (NC) = direction OPEN
//   run relay de-energized           = actuator stopped
//
// Drive mode: open-drain (LOW = relay ON, HIGH = pin high-impedance = relay
// OFF), see relaytest.cpp for details.
// ---------------------------------------------------------------------------

#define TRAVEL_TIME_SECONDS 140

struct Mixer {
  const char* name;
  int directionPin;
  int runPin;
  bool moving = false;
  bool movingToClosed = false;
  unsigned long moveStartMs = 0;
};

Mixer mixers[] = {
    // {name, directionPin, runPin} — wired: run relays on 16/18, direction relays on 17/19
    {"Mischer_FBHZ", 17, 16},
    {"Mischer_HK", 19, 18},
};
const int mixerCount = sizeof(mixers) / sizeof(mixers[0]);

const unsigned long travelMs = (unsigned long)TRAVEL_TIME_SECONDS * 1000UL * 115UL / 100UL;

void relayOn(int gpio) { digitalWrite(gpio, LOW); }   // sink to GND
void relayOff(int gpio) { digitalWrite(gpio, HIGH); } // high-impedance, circuit open

void stopMixer(Mixer& m) {
  relayOff(m.runPin);
  relayOff(m.directionPin);
  if (m.moving) {
    Serial.println(String(m.name) + ": gestoppt");
  }
  m.moving = false;
}

void startMixer(Mixer& m, bool toClosed) {
  stopMixer(m);
  delay(300);  // let the direction relay settle before powering the actuator
  if (toClosed) {
    relayOn(m.directionPin);
    delay(300);
  }
  relayOn(m.runPin);
  m.moving = true;
  m.movingToClosed = toClosed;
  m.moveStartMs = millis();
  Serial.println(String(m.name) + ": fahre " + (toClosed ? "ZU" : "AUF") +
                 " (" + String(travelMs / 1000) + "s)");
}

void printHelp() {
  Serial.println();
  Serial.println("=======================================================");
  Serial.println("MixerController mixer test");
  Serial.println("  1 = Mischer_FBHZ auffahren    3 = Mischer_HK auffahren");
  Serial.println("  2 = Mischer_FBHZ zufahren     4 = Mischer_HK zufahren");
  Serial.println("  0 = alle Antriebe sofort stoppen");
  Serial.println("  h = diese Hilfe");
  Serial.println("Laufzeit pro Fahrt: " + String(travelMs / 1000) + "s (" +
                 String(TRAVEL_TIME_SECONDS) + "s + 15% Reserve)");
  Serial.println("=======================================================");
}

void setup() {
  Serial.begin(115200);

  // Set the output latch to inactive BEFORE switching the pin to output,
  // so the relays never see an on-glitch during startup
  for (int i = 0; i < mixerCount; i++) {
    relayOff(mixers[i].runPin);
    relayOff(mixers[i].directionPin);
    pinMode(mixers[i].runPin, OUTPUT_OPEN_DRAIN);
    pinMode(mixers[i].directionPin, OUTPUT_OPEN_DRAIN);
    relayOff(mixers[i].runPin);
    relayOff(mixers[i].directionPin);
  }

  printHelp();
}

void loop() {
  if (Serial.available()) {
    char command = Serial.read();
    switch (command) {
      case '1': startMixer(mixers[0], false); break;
      case '2': startMixer(mixers[0], true); break;
      case '3': startMixer(mixers[1], false); break;
      case '4': startMixer(mixers[1], true); break;
      case '0':
        for (int i = 0; i < mixerCount; i++) stopMixer(mixers[i]);
        break;
      case 'h': printHelp(); break;
      default: break;  // ignore line breaks etc.
    }
  }

  unsigned long now = millis();
  static unsigned long lastProgressMs = 0;

  for (int i = 0; i < mixerCount; i++) {
    Mixer& m = mixers[i];
    if (!m.moving) continue;

    if (now - m.moveStartMs >= travelMs) {
      relayOff(m.runPin);
      relayOff(m.directionPin);
      m.moving = false;
      Serial.println(String(m.name) + ": Fahrt " + (m.movingToClosed ? "ZU" : "AUF") + " beendet");
    } else if (now - lastProgressMs >= 5000) {
      Serial.println(String(m.name) + ": " + (m.movingToClosed ? "ZU" : "AUF") + " seit " +
                     String((now - m.moveStartMs) / 1000) + "s / " + String(travelMs / 1000) + "s");
    }
  }
  if (now - lastProgressMs >= 5000) {
    lastProgressMs = now;
  }

  delay(20);
}
