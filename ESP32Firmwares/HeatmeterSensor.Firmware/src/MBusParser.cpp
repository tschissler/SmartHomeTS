#include "MBusParser.h"

namespace MBus {

// --- intern ---
static int dataLenFromDIF(uint8_t dif) {
  switch (dif & 0x0F) {
    case 0x00: return 0; // no data
    case 0x01: return 1;
    case 0x02: return 2;
    case 0x03: return 3;
    case 0x04: return 4;
    case 0x05: return 4; // float32 (nicht genutzt)
    case 0x06: return 6;
    case 0x07: return 8;
    default:   return 0;
  }
}

String decodeSerialHuman(const uint8_t* b4) {
  char s[9]; s[8] = 0;
  int j=0;
  for (int i=3;i>=0;i--) { // Byte-Reihenfolge umkehren
    s[j++] = char('0' + ((b4[i] >> 4) & 0xF));
    s[j++] = char('0' + (b4[i] & 0xF));
  }
  return String(s);
}

bool parseCi72(const std::vector<uint8_t>& pl, Result& out) {
  if (pl.size() < 15) return false;

  // Header: nach C(0) A(1) CI(2)
  memcpy(out.header.id,        &pl[3],  4);
  memcpy(out.header.man,       &pl[7],  2);
  out.header.version = pl[9];
  out.header.medium  = pl[10];
  out.header.accessNo= pl[11];
  out.header.status  = pl[12];
  memcpy(out.header.signature, &pl[13], 2);

  // Records ab Byte 15
  int i = 15;
  const int end = (int)pl.size();

  auto addReading = [&](int storage, const char* label, const String& val){
    out.readings.push_back({storage, String(label), val});
  };
  auto addInstant = [&](const char* label, const String& val){
    out.instants.push_back({String(label), val});
  };

  while (i < end) {
    uint8_t dif = pl[i++];

    if (dif == 0x00 || dif == 0x2F) break;

    // DIFE(s)
    int storage=0, tariff=0, devUnit=0, dfeIdx=0;
    while (dif & 0x80) {
      if (i >= end) break;
      uint8_t dfe = pl[i++];
      storage |= (dfe & 0x0F) << (4 * dfeIdx);
      tariff  |= ((dfe >> 4) & 0x03) << (2 * dfeIdx);
      if (dfe & 0x40) devUnit++;
      dfeIdx++;
      if ((dfe & 0x80)==0) break;
    }

    if (i >= end) break;
    uint8_t vif = pl[i++];
    // VIFE-Kette überspringen
    while (vif & 0x80) { if (i >= end) break; vif = pl[i++]; }

    const int dlen = dataLenFromDIF(dif);
    if (i + dlen > end) break;
    const uint8_t* data = &pl[i]; i += dlen;

    char buf[32];

    // ID (VIF 0x78)
    if (dlen==4 && vif==0x78) {
      out.idVif78 = decodeSerialHuman(data);
      continue;
    }

    // === ZÄHLERSTÄNDE ===
    if (dlen==4 && vif==0x06) { // 0.001 MWh
      uint32_t v = (uint32_t)data[0] | ((uint32_t)data[1]<<8) | ((uint32_t)data[2]<<16) | ((uint32_t)data[3]<<24);
      snprintf(buf, sizeof(buf), "%.3f MWh", v*0.001);
      addReading(storage, "Energie", String(buf));
      continue;
    }
    if (dlen==4 && vif==0x0E) { // 0.001 GJ
      uint32_t v = (uint32_t)data[0] | ((uint32_t)data[1]<<8) | ((uint32_t)data[2]<<16) | ((uint32_t)data[3]<<24);
      snprintf(buf, sizeof(buf), "%.3f GJ", v*0.001);
      addReading(storage, "Energie", String(buf));
      continue;
    }
    if (dlen==4 && vif==0x13) { // 0.001 m3
      uint32_t v = (uint32_t)data[0] | ((uint32_t)data[1]<<8) | ((uint32_t)data[2]<<16) | ((uint32_t)data[3]<<24);
      snprintf(buf, sizeof(buf), "%.3f m3", v*0.001);
      addReading(storage, "Volumen", String(buf));
      continue;
    }

    // === MOMENTANWERTE ===
    if (dlen==4 && vif==0x2B) { // 0.001 kW
      uint32_t v = (uint32_t)data[0] | ((uint32_t)data[1]<<8) | ((uint32_t)data[2]<<16) | ((uint32_t)data[3]<<24);
      snprintf(buf, sizeof(buf), "%.3f kW", v*0.001);
      addInstant("Leistung", String(buf));
      continue;
    }
    if (dlen==4 && vif==0x3B) { // 0.001 m3/h
      uint32_t v = (uint32_t)data[0] | ((uint32_t)data[1]<<8) | ((uint32_t)data[2]<<16) | ((uint32_t)data[3]<<24);
      snprintf(buf, sizeof(buf), "%.3f m3/h", v*0.001);
      addInstant("Durchfluss", String(buf));
      continue;
    }
    if (dlen==2 && vif==0x5B) { // Vorlauf 1 °C
      uint16_t v = (uint16_t)data[0] | ((uint16_t)data[1]<<8);
      snprintf(buf, sizeof(buf), "%u C", (unsigned)v);
      addInstant("Vorlauf", String(buf));
      continue;
    }
    if (dlen==2 && vif==0x5F) { // Rücklauf 1 °C
      uint16_t v = (uint16_t)data[0] | ((uint16_t)data[1]<<8);
      snprintf(buf, sizeof(buf), "%u C", (unsigned)v);
      addInstant("Rücklauf", String(buf));
      continue;
    }
    if (dlen==2 && vif==0x61) { // ΔT 0.01 °C
      uint16_t v = (uint16_t)data[0] | ((uint16_t)data[1]<<8);
      snprintf(buf, sizeof(buf), "%.2f C", v/100.0);
      addInstant("DeltaT", String(buf));
      continue;
    }

    // Unbekannt -> (optional ignorieren oder loggen)
    // Hier bewusst weggelassen, um Ausgabe schlank zu halten.
  }

  return true;
}

void printResult(const Result& r, Stream& out) {
  out.println("Header:");
  out.print("  ID: "); out.print(decodeSerialHuman(r.header.id));
  out.print(" (raw "); for (int i=0;i<4;i++){ if(i) out.print("-"); if(r.header.id[i]<16) out.print("0"); out.print(r.header.id[i], HEX);} out.println(")");
  out.print("  Man: "); if(r.header.man[0]<16) out.print("0"); out.print(r.header.man[0],HEX); out.print("-");
  if(r.header.man[1]<16) out.print("0"); out.print(r.header.man[1],HEX);
  out.print("  Ver:"); out.print(r.header.version);
  out.print("  Med:0x"); out.print(r.header.medium, HEX);
  out.print("  Access:"); out.print(r.header.accessNo);
  out.print("  Status:0x"); out.println(r.header.status, HEX);

  if (r.idVif78.length()) { out.print("  ID(VIF 0x78): "); out.println(r.idVif78); }

  out.println("\nZählerstände:");
  if (r.readings.empty()) out.println("  (keine bekannten Zählerstände erkannt)");
  else for (const auto& x : r.readings) {
    out.print("  S"); out.print(x.storage); out.print(": ");
    out.print(x.label); out.print(" = "); out.println(x.value);
  }

  out.println("\nMomentanwerte:");
  if (r.instants.empty()) out.println("  (keine bekannten Momentanwerte erkannt)");
  else for (const auto& x : r.instants) {
    out.print("  "); out.print(x.label); out.print(" = "); out.println(x.value);
  }
}

}
