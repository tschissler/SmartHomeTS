#ifndef MBUSPARSER_H
#define MBUSPARSER_H

#include <Arduino.h>

extern bool debug;

// forward declarations
struct MBusParsingResult;
struct MBusHeader;
struct MBusData;
struct ManufacturerInfo;
struct MBusDoubleValue;
struct MBusIntValue;
struct ManufacturerCodeName;
struct MBusDateTime;

MBusParsingResult ParseMBusFrame(const uint8_t *frame, int length);
String MediumCodeToString(uint8_t medium);
String StatusByteToString(uint8_t status);

// Struct to hold manufacturer info
struct ManufacturerInfo {
    String code;
    String name;
};

struct MBusDoubleValue {
    double value;
    String unit;
    bool hasValue;
    
    MBusDoubleValue() : value(0.0), unit(""), hasValue(false) {}
    MBusDoubleValue(double v, const String& u) : value(v), unit(u), hasValue(true) {}
};

struct MBusIntValue {
    int32_t value;
    String unit;
    bool hasValue;
    
    MBusIntValue() : value(0), unit(""), hasValue(false) {}
    MBusIntValue(int32_t v, const String& u) : value(v), unit(u), hasValue(true) {}
};

struct MBusDateTime {
  int16_t year;    // e.g., 2025
  uint8_t month;   // 1..12
  uint8_t day;     // 1..31
  uint8_t hour;    // 0..23
  uint8_t minute;  // 0..59  (63 may be sentinel in some frames)
  bool summer_time; // SU bit
  bool invalid;     // IV bit
};

struct MBusData {
    String deviceId;
    MBusDoubleValue totalHeatEnergy;
    MBusDoubleValue currentValue;
    MBusDoubleValue heatmeterHeat;
    MBusDoubleValue heatCoolingmeterHeat;
    MBusDoubleValue totalVolume;
    MBusDoubleValue powerCurrentValue;
    MBusDoubleValue powerMaximumValue;
    MBusIntValue flowCurrentValue;
    MBusIntValue flowMaximumValue;
    MBusIntValue forwardFlowTemperature;
    MBusIntValue returnFlowTemperature;
    MBusDoubleValue temperatureDifference;
    MBusIntValue daysInOperation;
    MBusDateTime currentDateAndTime;
    String status;
};

// Struct to hold M-Bus header data
struct MBusHeader {
    String id;
    ManufacturerInfo manufacturer;
    uint8_t version;
    uint8_t medium;
    uint8_t accessNo;
    uint8_t status;
    uint16_t signature;
};

// Struct to hold the result of M-Bus parsing
struct MBusParsingResult {
    MBusHeader header;
    MBusData data;
    bool hasValue = false;
};

// Helper: convert the 2-byte manufacturer code (M-Bus) into a 3-letter ASCII manufacturer ID and name.
struct ManufacturerCodeName {
    const char* code;
    const char* name;
};
#endif
