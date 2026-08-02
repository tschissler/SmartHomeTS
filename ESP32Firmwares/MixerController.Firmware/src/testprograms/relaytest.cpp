#include <Arduino.h>

// ---------------------------------------------------------------------------
// Relay wiring test
//
// Cycles all connected relay channels sequentially: each relay is switched
// on for 1 second, then off, then the next one follows. Serial output shows
// which GPIO is currently active, so wiring and reliable switching can be
// verified channel by channel.
//
// Drive mode: open-drain. The board's 5V-referenced opto inputs cannot be
// released by an actively driven 3.3V high level, but they do release when
// the pin goes high-impedance (circuit open, no LED current — the same state
// the pins have during reset). In open-drain mode LOW sinks the input to GND
// (relay ON), HIGH releases the pin to high-impedance (relay OFF).
// ---------------------------------------------------------------------------

struct RelayChannel {
  int gpio;
  const char* label;
};

const RelayChannel channels[] = {
    {16, "Mischer 1 Fahrt"},
    {17, "Mischer 1 Richtung"},
    {18, "Mischer 2 Fahrt"},
    {19, "Mischer 2 Richtung"},
    {21, "Reserve 1"},
    {22, "Reserve 2"},
    {23, "Reserve 3"},
    {32, "Reserve 4"},
    {33, "Reserve 5"},
};
const int channelCount = sizeof(channels) / sizeof(channels[0]);

const unsigned long ON_TIME_MS = 50;
const unsigned long OFF_TIME_MS = 30;
const unsigned long PAUSE_BETWEEN_CYCLES_MS = 50;

void relayOn(int gpio) { digitalWrite(gpio, LOW); }   // sink to GND
void relayOff(int gpio) { digitalWrite(gpio, HIGH); } // high-impedance, circuit open

void setup() {
  Serial.begin(115200);

  // Set the output latch to inactive BEFORE switching the pin to output,
  // so the relays never see an on-glitch during startup
  for (int i = 0; i < channelCount; i++) {
    relayOff(channels[i].gpio);
    pinMode(channels[i].gpio, OUTPUT_OPEN_DRAIN);
    relayOff(channels[i].gpio);
  }

  Serial.println();
  Serial.println("=======================================================");
  Serial.println("MixerController relay wiring test");
  Serial.println("All relays off. Cycling through " + String(channelCount) + " channels...");
  Serial.println("=======================================================");
}

void loop() {
  static int cycle = 1;

  Serial.println();
  Serial.println("--- Cycle " + String(cycle) + " ---");

  for (int i = 0; i < channelCount; i++) {
    Serial.printf("GPIO %2d ON   (%s)\n", channels[i].gpio, channels[i].label);
    relayOn(channels[i].gpio);
    delay(ON_TIME_MS);
    relayOff(channels[i].gpio);
    Serial.printf("GPIO %2d off\n", channels[i].gpio);
    delay(OFF_TIME_MS);
  }

  Serial.println("Cycle " + String(cycle) + " done, pausing...");
  cycle++;
  delay(PAUSE_BETWEEN_CYCLES_MS);
}
