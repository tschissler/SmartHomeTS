#include <Arduino.h>
#include <deque>
#include "SerialComm.h"
#include "MBusParser.h"
#include <WiFiUdp.h>
#include <NTPClient.h>
#include <ESP32Ping.h>
#include <esp_mac.h> 
#include <memory>
#include <ArduinoJson.h>

#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"

const int irLedPin = D8;              // IR LED (TX)
const int irPhototransistorPin = D5;  // IR phototransistor (RX)
const int ledPin_Blue = D10;// 18;
const int ledPin_Green = D9;  
const int ledPin_Red = D7;  

static const uint32_t BAUD = 2400;        // Operational baud (8E1)
static const int PREAMBLE_BYTES = 504;    // Wake/preamble length
static const char SERIAL_NR[] = "51158148";

void setup() {
  pinMode(irLedPin, OUTPUT);
  pinMode(irPhototransistorPin, INPUT);
  pinMode(ledPin_Blue, OUTPUT);
  pinMode(ledPin_Green, OUTPUT);
  pinMode(ledPin_Red, OUTPUT);
  digitalWrite(irLedPin, LOW);
  Serial.begin(115200);
  delay(50);
  Serial.println();
}

void loop() {

  SerialComm::Config cfg;
  cfg.gpioRx = irPhototransistorPin;
  cfg.gpioTx = irLedPin;
  cfg.invertLogic = false;
  cfg.baud = BAUD;
  cfg.preambleBytes = PREAMBLE_BYTES;
  cfg.serialNumber = SERIAL_NR;

  SerialComm serialComm(cfg);
  serialComm.ReadData();
  delay(2500);
  
}
