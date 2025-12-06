#include <Arduino.h>
#include <OneWire.h>
#include <cstdio>

// OneWire DS18S20, DS18B20, DS1822 Temperature Example
//
// http://www.pjrc.com/teensy/td_libs_OneWire.html
//
// The DallasTemperature library can do all this work for you!
// https://github.com/milesburton/Arduino-Temperature-Control-Library

OneWire  ds(18);  // on pin 18 (a 4.7K resistor is necessary)

static uint8_t previousLineCount = 0;

static void moveCursorUp(uint8_t lines) {
  if (lines == 0) {
    return;
  }
  Serial.write(27);  // ESC
  Serial.print("[");
  Serial.print(lines);
  Serial.print("A");
}

static void clearLine() {
  Serial.write(27);  // ESC
  Serial.print("[2K");
  Serial.write('\r');
}

void setup(void) {
  Serial.begin(115200);
}

void loop(void) {
  byte addr[8];
  byte data[9];
  bool headerPrinted = false;
  uint8_t lineCount = 0;

  moveCursorUp(previousLineCount);
  ds.reset_search();

  while (ds.search(addr)) {
    if (!headerPrinted) {
      clearLine();
      Serial.println("Chip     | Resolution | Address                        | Celsius");
      lineCount++;
      clearLine();
      Serial.println("----------------------------------------------------------------------------");
      lineCount++;
      headerPrinted = true;
    }

    if (OneWire::crc8(addr, 7) != addr[7]) {
      Serial.println("Invalid address CRC, skipping sensor.");
      continue;
    }

    byte type_s = 0;
    const char *chipName = nullptr;

    switch (addr[0]) {
      case 0x10:
        chipName = "DS18S20";
        type_s = 1;
        break;
      case 0x28:
        chipName = "DS18B20";
        break;
      case 0x22:
        chipName = "DS1822";
        break;
      default:
        Serial.println("Unknown sensor family, skipping device.");
        continue;
    }

    ds.reset();
    ds.select(addr);
    ds.write(0x44, 1);  // start conversion, parasite power on at the end

    delay(750);

    ds.reset();
    ds.select(addr);
    ds.write(0xBE);  // read scratchpad

    for (byte i = 0; i < 9; i++) {
      data[i] = ds.read();
    }

    int16_t raw = (data[1] << 8) | data[0];
    uint8_t resolutionBits = 12;
    if (type_s) {
      raw <<= 3;  // 9 bit resolution default for DS18S20
      resolutionBits = 9;
      if (data[7] == 0x10) {
        raw = (raw & 0xFFF0) + 12 - data[6];
      }
    } else {
      byte cfg = (data[4] & 0x60);
      if (cfg == 0x00) {
        raw &= ~7;
        resolutionBits = 9;
      } else if (cfg == 0x20) {
        raw &= ~3;
        resolutionBits = 10;
      } else if (cfg == 0x40) {
        raw &= ~1;
        resolutionBits = 11;
      } else {
        resolutionBits = 12;
      }
    }

    float celsius = static_cast<float>(raw) / 16.0f;

    char addressHex[3 * 8];
    char *addrCursor = addressHex;
    for (byte i = 0; i < 8; i++) {
      sprintf(addrCursor, "%02X", addr[i]);
      addrCursor += 2;
      if (i < 7) {
        *addrCursor++ = '-';
      }
    }
    *addrCursor = '\0';

    char resolutionBuf[12];
    snprintf(resolutionBuf, sizeof(resolutionBuf), "%u-bit", resolutionBits);

    clearLine();
    Serial.printf("%-8s | %-10s | %-29s | %6.2f\r\n", chipName, resolutionBuf, addressHex, celsius);
    lineCount++;
  }

  if (!headerPrinted) {
    clearLine();
    Serial.println("No DS18x20 sensors found.");
    lineCount = 1;
  }

  Serial.println("                                                                             ");
  lineCount++;
  Serial.println("                                                                             ");
  lineCount++;
  previousLineCount = lineCount;
  delay(1000);
}