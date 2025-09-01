#ifndef MBUSPARSER_H
#define MBUSPARSER_H

#include <Arduino.h>

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
    uint32_t deviceId;
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

class MBusParser {
private:
    // Static manufacturer table
    static const ManufacturerCodeName manufacturerTable[];
    
    // List of VIFs that have VIFE (Value Information Field Extension)
    static const uint8_t VIFs_WITH_VIFE[];
    
    // Private helper methods
    static bool vifHasVife(uint8_t vif);
    static ManufacturerInfo manufacturerInfoFromCode(uint16_t manCode);
    static unsigned long bcdToUInt(const uint8_t *data, int length);
    
    // M-Bus data type conversion methods
    static String DecodeTypeA_BCD(const uint8_t* d);
    static uint32_t DecodeTypeA_UInt32(const uint8_t* d);
    static int32_t DecodeTypeB(const uint8_t* d, size_t size);
    static bool DecodeTypeC(const uint8_t* d, size_t nBytes, uint64_t& value);
    static uint64_t DecodeTypeD(const uint8_t* d, size_t nBytes);
    static MBusDateTime DecodeTypeF(const uint8_t* d);
    
    static MBusHeader parseHeaderInfo(const uint8_t *frame, int length, int &index);
    static MBusData parseMBusData(const uint8_t *frame, int length, int &index);

    static bool ExtractDataFromFrame(uint8_t dataLen, int &index, int length, uint8_t dataBytes[8], const uint8_t *frame);

public:
    static bool debug;

    // Main parsing method
    static MBusParsingResult parseMBusFrame(const uint8_t *frame, int length);
    
    // Utility methods
    static String mediumCodeToString(uint8_t medium);
    static String statusByteToString(uint8_t status);
    static void printHeaderInfo(const MBusHeader &header);
    static void printMBusData(const MBusData &data);
};

#endif
