#include "SerialComm.h"

bool SerialComm::debugLogAll = false;

SerialComm::SerialComm(const Config& cfg) : _hs(nullptr), _cfg(cfg) {
  // Choose UART
  int idx = (_cfg.uartIndex == 2) ? 2 : 1; // limit to 1 or 2
  _hs = (idx == 2) ? &Serial2 : &Serial1;
}

void SerialComm::ReadData() {
  sendWakeup();
  sendShort(0x40, 0xFE); // SND_NKE
  delay(100);
  tryReadAck("SND_NKE");

  sendWakeup();
  sendSelectBySecondary(_cfg.serialNumber.c_str());
  delay(100);
  tryReadAck("SND_Secondary");

  sendWakeup();
  sendShort(0x5B, 0xFD); // REQ_UD2
  delay(100);
  readLongFrame(_payload, _ci);
}


void SerialComm::sendWakeup() {
  begin8N1();
  for (int i=0; i<_cfg.preambleBytes; ++i) _hs->write(0x55);
  _hs->flush();
  Serial.println("Wake-up: " + String(_cfg.preambleBytes) + " x 0x55 sent @ 8N1");
  delay(120); 
}

void SerialComm::sendShort(uint8_t C, uint8_t A) {
  begin8E1();
  const uint8_t cs = (uint8_t)((C + A) & 0xFF);
  const uint8_t f[5] = {0x10, C, A, cs, 0x16};
  _hs->write(f, sizeof(f)); 
  _hs->flush();
  Serial.print("TX Short: "); 
  dumpHex(f, sizeof(f), Serial); 
  Serial.println();
}

void SerialComm::sendSelectBySecondary(const char* serial8) {
  uint8_t id4[4]; 

  begin8E1();
  
  encodeSerialTypeA(serial8, id4);

  // user = C,A,CI + ID(4) + Man(2)=FF FF + Ver(1)=FF + Med(1)=FF
  uint8_t user[3+4+2+1+1];
  size_t idx = 0;
  user[idx++] = 0x53; // C
  user[idx++] = 0xFD; // A
  user[idx++] = 0x52; // CI
  memcpy(&user[idx], id4, 4); idx += 4;
  user[idx++] = 0xFF; user[idx++] = 0xFF; // Manufacturer wildcard
  user[idx++] = 0xFF; // Version
  user[idx++] = 0xFF; // Medium

  const uint8_t len = (uint8_t)idx;
  const uint8_t cs  = checksum(user, idx);
  const uint8_t hdr[4] = {0x68, len, len, 0x68};

  _hs->write(hdr, 4);
  _hs->write(user, idx);
  _hs->write(cs);
  _hs->write(0x16);
  _hs->flush();

  Serial.print("TX Select: ");
  dumpHex(hdr, 4, Serial); Serial.print("-");
  dumpHex(user, idx, Serial); Serial.print("-");
  if (cs < 16) Serial.print("0"); Serial.print(cs, HEX);
  Serial.println("-16");
}

bool SerialComm::tryReadAck(String label) {
  uint8_t b=0;
  if (!readByte(b, 300)) {
    Serial.println("tryReadAck for " + String(label) + ": timeout");
    return false;
  }
  Serial.print(String(label) + ": ACK received 0x"); 
  Serial.println(b, HEX);
  return true;
}

bool SerialComm::readByte(uint8_t& b, uint32_t timeoutMs) {
  const uint32_t start = millis();
  while (millis() - start < timeoutMs) {
    if (_hs->available() > 0) {
      b = (uint8_t)_hs->read();
      if (debugLogAll) {
        Serial.print("<RX "); if (b<16) Serial.print('0'); Serial.print(b, HEX); Serial.print(" @"); Serial.println(millis());
      }
      return true; }
    delay(1);
  }
  return false;
}

void SerialComm::begin8N1() {
  _hs->begin(_cfg.baud, SERIAL_8N1, _cfg.gpioRx, _cfg.gpioTx, _cfg.invertLogic);
}

void SerialComm::begin8E1() {
  _hs->begin(_cfg.baud, SERIAL_8E1, _cfg.gpioRx, _cfg.gpioTx, _cfg.invertLogic);
}

void SerialComm::dumpHex(const uint8_t* d, size_t n, Stream& out) {
  for (size_t i=0;i<n;i++) {
    if (i) out.print("-");
    if (d[i] < 16) out.print("0");
    out.print(d[i], HEX);
  }
}

void SerialComm::sniff(uint32_t windowMs, const char* label) {
  Serial.print("Sniff "); Serial.print(label); Serial.print(" window="); Serial.print(windowMs); Serial.println("ms");
  uint32_t start = millis();
  uint32_t lastPrint = 0;
  while (millis() - start < windowMs) {
    while (_hs->available()) {
      uint8_t b = _hs->read();
      Serial.print(b<16?" ":" "); if (b<16) Serial.print('0'); Serial.print(b, HEX);
      lastPrint = millis();
    }
    if (millis() - lastPrint > 200) delay(1);
  }
  Serial.println();
}

void SerialComm::encodeSerialTypeA(const char* ser8, uint8_t out4[4]) {
  // expects 8 digits; empty => '0'
  char s[9] = "00000000";
  if (ser8) {
    size_t L = strlen(ser8);
    for (int i=0;i<8;i++) {
      char c = (L>=8) ? ser8[i] : s[i];
      s[i] = (c>='0' && c<='9') ? c : '0';
    }
  }
  // LSB-first: Byte0 = Digits 7..6, then 5..4, 3..2, 1..0
  out4[0] = toBcd(s[6], s[7]);
  out4[1] = toBcd(s[4], s[5]);
  out4[2] = toBcd(s[2], s[3]);
  out4[3] = toBcd(s[0], s[1]);
}

uint8_t SerialComm::toBcd(char tens, char ones) {
  return (uint8_t)(((tens - '0') << 4) | (ones - '0'));
}



















bool SerialComm::readBytes(uint8_t* dst, size_t len, uint32_t timeoutPerByteMs) {
  for (size_t i=0;i<len;i++) if (!readByte(dst[i], timeoutPerByteMs)) return false;
  return true;
}

uint8_t SerialComm::checksum(const uint8_t* b, size_t n) {
  uint32_t s=0; for (size_t i=0;i<n;i++) s += b[i]; return (uint8_t)(s & 0xFF);
}













bool SerialComm::readLongFrame(std::vector<uint8_t>& payload, uint8_t& ci) {
  begin8E1();

  uint8_t b=0;
  // sync auf 0x68
  uint32_t start = millis();
  while (true) {
    if (millis() - start > 3000) { Serial.println("Timeout while reading LongFrame"); return false; }
    if (readByte(b, 10) && b == 0x68) break;
  }

  uint8_t l1=0, l2=0, start2=0;
  if (!readByte(l1, 200) || !readByte(l2, 200) || !readByte(start2, 200)) return false;
  if (l1 != l2 || start2 != 0x68) { Serial.println("Len/Start fehlerhaft"); return false; }

  payload.resize(l1);
  if (!readBytes(payload.data(), l1, 200)) return false;

  uint8_t cs=0, stop=0;
  if (!readByte(cs, 200) || !readByte(stop, 200)) return false;
  if (stop != 0x16) { Serial.println("Stop 0x16 fehlt"); return false; }

  const uint8_t calc = checksum(payload.data(), payload.size());
  if (calc != cs) {
    Serial.print("Checksumme falsch, exp="); Serial.print(calc, HEX);
    Serial.print(" got="); Serial.println(cs, HEX);
    return false;
  }

  Serial.print("RX Long: len="); Serial.print(payload.size());
  Serial.print(" C=0x"); Serial.print(payload[0], HEX);
  Serial.print(" A=0x"); Serial.print(payload[1], HEX);
  Serial.print(" CI=0x"); Serial.println(payload[2], HEX);

  ci = payload[2];
  return true;
}
