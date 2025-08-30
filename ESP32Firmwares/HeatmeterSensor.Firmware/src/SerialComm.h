#pragma once
#include <Arduino.h>
#include <SoftwareSerial.h>
#include <vector>

class SerialComm {
public:
  struct Config {
    int      gpioRx;           
    int      gpioTx;           
    bool     invertLogic;      // true, wenn dein RX-Signal invertiert ist
    uint32_t baud;             
    int      preambleBytes;   
    String   serialNumber;    // 8-stellige Seriennummer, leer = keine Selektion
  };

  explicit SerialComm(const Config& cfg);
 
  void ReadData();

  // Wake-up: 504×0x55 @ 8N1
  void sendWakeup();

  // M-Bus Short-Frame: 0x10 C A CS 0x16 (8E1)
  void sendShort(uint8_t C, uint8_t A);

  // Select-by-Secondary (A=0xFD, CI=0x52) mit 8-stelliger Seriennummer
  void sendSelectBySecondary(const char* serial8);

  // ACK 0xE5 erwarten (true bei Erfolg)
  bool tryReadAck(String label);

  // Long-Frame empfangen: 68 L L 68 [payload L] CS 16 (8E1)
  // Liefert payload und CI (payload[0]=C, [1]=A, [2]=CI).
  bool readLongFrame(std::vector<uint8_t>& payload, uint8_t& ci);

  // Debug-Helfer
  static void dumpHex(const uint8_t* d, size_t n, Stream& out);

private:
  void begin8N1();
  void begin8E1();

  bool readByte(uint8_t& b, uint32_t timeoutMs);
  bool readBytes(uint8_t* dst, size_t len, uint32_t timeoutPerByteMs);
  static uint8_t checksum(const uint8_t* b, size_t n);
  static uint8_t toBcd(char tens, char ones);
  static void    encodeSerialTypeA(const char* ser8, uint8_t out4[4]);

private:
  SoftwareSerial _sw;
  Config         _cfg;
  std::vector<uint8_t> _payload;
  uint8_t _ci;
  // kleiner RX-Puffer für SoftwareSerial
  static constexpr uint16_t RX_BUF_LEN = 256;
};
