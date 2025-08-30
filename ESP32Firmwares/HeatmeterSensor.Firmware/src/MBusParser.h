#pragma once
#include <Arduino.h>
#include <vector>

namespace MBus {

struct Header {
  uint8_t id[4];      // BCD Type-A, LSB-first
  uint8_t man[2];
  uint8_t version;
  uint8_t medium;
  uint8_t accessNo;
  uint8_t status;
  uint8_t signature[2];
};

struct Reading {  // Zählerstand (mit Storage S)
  int     storage;   // S0=Sofort, S1=Stichtag usw.
  String  label;     // "Energie", "Volumen" …
  String  value;     // "0.123 MWh" …
};

struct Instant {   // Momentanwerte
  String  label;   // "Leistung", "Durchfluss", "Vorlauf" …
  String  value;   // "1.234 kW" …
};

struct Result {
  Header              header;
  std::vector<Reading> readings;
  std::vector<Instant> instants;
  String              idVif78;  // optional zweite ID aus VIF 0x78
};

// BCD-Seriennummer hübsch darstellen
String decodeSerialHuman(const uint8_t* b4);

// CI=0x72 Payload parsen (payload: C A CI ...)
// Füllt result; gibt true bei Erfolg.
bool parseCi72(const std::vector<uint8_t>& payload, Result& out);

// Für schnelle Ausgabe
void printResult(const Result& r, Stream& out);

} 
