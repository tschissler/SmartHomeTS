#include <Arduino.h>
#include <OneWire.h>
#include <cstdio>

const int fanPWM = 4; // GPIO pin connected to the base of the transistor
const int tachPin = 21; // GPIO pin connected to the tachometer pin of the fan (optional)
const int ds18b20Pin = 5; // GPIO pin connected to the DS18B20 sensor data pin
const int freq = 5000; // PWM frequency
const int resolution = 8; // 8-bit resolution (0-255)

OneWire  ds(ds18b20Pin);  // on pin 5 (a 4.7K resistor is necessary)
volatile int pulseCount = 0;

static uint8_t previousLineCount = 0;
static uint32_t lastRpmSampleMs = 0; // Tracks time of last RPM calculation

void IRAM_ATTR pulseCounter() {
  pulseCount = pulseCount + 1;
}

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
  if (tachPin != -1) {
    pinMode(tachPin, INPUT_PULLUP);
    attachInterrupt(digitalPinToInterrupt(tachPin), pulseCounter, FALLING);
  }
  Serial.begin(115200);
  ledcAttach(fanPWM, freq, resolution);
}

void loop(void) {
  byte addr[8];
  byte data[9];
  bool headerPrinted = false;
  uint8_t lineCount = 0;

  ledcWrite(fanPWM, 255);
  moveCursorUp(previousLineCount);
  ds.reset_search();

  while (ds.search(addr)) {
    if (!headerPrinted) {
      clearLine();
      Serial.println("Chip     | Resolution | Data (hex)                               | Celsius");
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

    char dataHex[3 * 9];
    char *cursor = dataHex;
    for (byte i = 0; i < 9; i++) {
      sprintf(cursor, "%02X", data[i]);
      cursor += 2;
      if (i < 8) {
        *cursor++ = ' ';
      }
    }
    *cursor = '\0';

    char resolutionBuf[12];
    snprintf(resolutionBuf, sizeof(resolutionBuf), "%u-bit", resolutionBits);

    clearLine();
    Serial.printf("%-8s | %-10s | %-40s | %6.2f\r\n", chipName, resolutionBuf, dataHex, celsius);
    lineCount++;
  }

  if (!headerPrinted) {
    clearLine();
    Serial.println("No DS18x20 sensors found.");
    lineCount = 1;
  }

  const uint32_t nowMs = millis();
  uint32_t elapsedMs = 0;
  if (lastRpmSampleMs != 0) {
    elapsedMs = nowMs - lastRpmSampleMs;
  }

  int rpm = 0;
  if (elapsedMs > 0) {
    const int pulses = pulseCount;
    rpm = static_cast<int>((static_cast<int64_t>(pulses) * 30000) / elapsedMs);
  }

  pulseCount = 0;
  lastRpmSampleMs = nowMs;
  Serial.print("Fan RPM: ");
  Serial.println(rpm);
  lineCount++;
  previousLineCount = lineCount;
  delay(1000);
}