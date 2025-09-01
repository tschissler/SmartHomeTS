#include "MBusParser.h"

// This code is parsing M-Bus frames from the WDV Molliné Wingstar meter.
// The documentation of the WDV Molliné Wingstar meter can be found here:
// https://www.molline.de/fileadmin/content/content/Downloads/Produkte/02_W%C3%A4rmez%C3%A4hler/01_Kompaktz%C3%A4hler/01_WingStar_C3_Familie/M-Bus_Protokoll_Ultramess_C3_WingStar_C3.pdf

// Global debug variable definition
bool debug = false;

// Helper function to check if a VIF has a VIFE
static bool vifHasVife(uint8_t vif) {
    for (size_t i = 0; i < sizeof(VIFs_WITH_VIFE); ++i) {
        if (vif == VIFs_WITH_VIFE[i]) {
            return true;
        }
    }
    return false;
}

ManufacturerInfo manufacturerInfoFromCode(uint16_t manCode) {
    char letters[4];
    letters[0] = ((manCode >> 10) & 0x1F) + 'A' - 1;
    letters[1] = ((manCode >> 5) & 0x1F) + 'A' - 1;
    letters[2] = (manCode & 0x1F) + 'A' - 1;
    letters[3] = '\0';
    String codeStr = String(letters);
    String nameStr = "Unknown";
    for (size_t i = 0; i < sizeof(manufacturerTable)/sizeof(manufacturerTable[0]); ++i) {
        if (codeStr == manufacturerTable[i].code) {
            nameStr = manufacturerTable[i].name;
            break;
        }
    }
    return ManufacturerInfo{codeStr, nameStr};
}

// Helper: convert medium code (M-Bus) into a human-readable string.
String MediumCodeToString(uint8_t medium)
{
    switch (medium)
    {
    case 0x02:
        return("Electricity");
        break;
    case 0x03:
        return("Gas");
        break;
    case 0x04:
        return("Heat");
        break;
    case 0x06:
        return("Hot water");
        break;
    case 0x07:
        return("Water");
        break;
    case 0x08:
        return("Heat cost allocator");
        break;
    case 0x0A:
        return("Cooling (outlet)");
        break;
    case 0x0B:
        return("Cooling (inlet)");
        break;
    case 0x0C:
        return("Heat (inlet)");
        break;
    case 0x0D:
        return("Heat/Cooling combined");
        break;
    default:
        break;
    }
    return("Unknown");
}

// Helper: translate status byte into human-readable text
String StatusByteToString(uint8_t status) {
    String result = "";
    if (status & 0x01) result += "Temperature sensor 1: cable broken; ";
    if (status & 0x02) result += "Temperature sensor 1: short circuit; ";
    if (status & 0x04) result += "Temperature sensor 2: cable broken; ";
    if (status & 0x08) result += "Temperature sensor 2: short circuit; ";
    if (status & 0x10) result += "Error in flow measurement system / coil error; ";
    if (status & 0x20) result += "Electronics defective; ";
    if (status & 0x40) result += "Reset; ";
    if (status & 0x80) result += "Low battery; ";
    if (result.length() == 0) result = "OK";
    return result;
}

// ---------- Helpers ----------
static inline uint64_t read_u_le(const uint8_t* d, size_t n) {
  uint64_t v = 0;
  for (size_t i = 0; i < n; ++i) v |= (uint64_t)d[i] << (8 * i);
  return v;
}
static inline int64_t read_s_le(const uint8_t* d, size_t n) {
  uint64_t u = read_u_le(d, n);
  if (n > 0 && (d[n - 1] & 0x80)) { // sign bit set in most-significant byte?
    u |= (~0ULL) << (8 * n);       // sign-extend
  }
  return (int64_t)u;
}

// ---------- Type A (32-bit packed BCD, 8 digits) ----------
/* Decodes 4 bytes of packed BCD (serial numbers etc.).
 * Returns false if any nibble is not 0..9.
 * Output keeps leading zeros.
 */
String DecodeTypeA(const uint8_t* d) {
    String out;
    out.reserve(8);
    out = "";
    for (int i = 3; i >= 0; --i) {
        uint8_t b = d[i];
        uint8_t hi = (b >> 4) & 0x0F;
        uint8_t lo =  b       & 0x0F;
        if (hi > 9 || lo > 9 || hi < 0 || lo < 0) {
            Serial.print("Invalid BCD nibble: ");
            Serial.print(hi);
            Serial.print(", ");
            Serial.println(lo);
            return "";
        }
        out += char('0' + hi);
        out += char('0' + lo);
    }
    return out;
}

// ---------- Type B (signed binary, two's complement) ----------
/* nBytes must be 1..8 */
int64_t DecodeTypeB(const uint8_t* d, size_t size) {
  return read_s_le(d, size);
}

// ---------- Type C (unsigned binary) ----------
/* nBytes must be 1..8 */
bool DecodeTypeC(const uint8_t* d, size_t nBytes, uint64_t& value) {
  if (nBytes == 0 || nBytes > 8) return false;
  value = read_u_le(d, nBytes);
  return true;
}

// ---------- Type D (bit field) ----------
/* Returns the bitfield as an unsigned integer (LSB = bit 0). */
uint64_t DecodeTypeD(const uint8_t* d, size_t nBytes) {
  if (nBytes == 0 || nBytes > 8) return 0;
  return read_u_le(d, nBytes);
}

// ---------- Type F (CP32: Date + Time in 4 bytes) ----------
/* Byte 0: bits0..5 minute, bit7 IV
 * Byte 1: bits0..4 hour, bits5..6 HY (hundred-year), bit7 SU
 * Byte 2: bits0..4 day,  bits5..7 Y2..Y0
 * Byte 3: bits0..3 month, bits4..7 Y6..Y3
 * year = 1900 + 100*HY + (Y6..Y0)
 */
MBusDateTime DecodeTypeF(const uint8_t* d) {
  MBusDateTime ts;
  uint8_t b0 = d[0], b1 = d[1], b2 = d[2], b3 = d[3];

  ts.minute      =  b0 & 0x3F;
  ts.invalid     = (b0 & 0x80) != 0;
  ts.summer_time = (b1 & 0x80) != 0;
  ts.hour        =  b1 & 0x1F;
  int HY         = (b1 >> 5) & 0x03;

  ts.day   =  b2 & 0x1F;
  int ylo3 = (b2 >> 5) & 0x07;        // Y2..Y0
  ts.month =  b3 & 0x0F;
  int yhi4 = (b3 >> 4) & 0x0F;        // Y6..Y3
  int y7   = (yhi4 << 3) | ylo3;      // 0..99
  ts.year  = (int16_t)(1900 + HY * 100 + y7);

  // Basic sanity check (optional, still return true so caller can decide)
  if (!(ts.month >= 1 && ts.month <= 12) && (ts.day >= 1 && ts.day <= 31) && (ts.hour <= 23) && (ts.minute <= 63)) {
    Serial.println("Warning: Invalid date/time values in Type F");
  }

  return ts;
}

// ---------- Type G (CP16: Date in 2 bytes) ----------
struct MBusDate {
  int16_t year;   // mapped to 4-digit year
  uint8_t month;  // 1..12
  uint8_t day;    // 1..31
};

/* Byte 0: bits0..4 day,  bits5..7 Y2..Y0
 * Byte 1: bits0..3 month, bits4..7 Y6..Y3
 * y99 = Y6..Y0 (0..99). Mapping: 00..80 -> 2000..2080, else 1900+Y.
 */
bool DecodeTypeG(const uint8_t* d, MBusDate& date) {
  uint8_t b0 = d[0], b1 = d[1];
  date.day   =  b0 & 0x1F;
  int ylo3   = (b0 >> 5) & 0x07;
  date.month =  b1 & 0x0F;
  int yhi4   = (b1 >> 4) & 0x0F;
  int y99    = (yhi4 << 3) | ylo3;

  date.year = (y99 <= 80) ? (int16_t)(2000 + y99) : (int16_t)(1900 + y99);

  bool ok = (date.month >= 1 && date.month <= 12) && (date.day >= 1 && date.day <= 31);
  return ok;
}

// Private helper function to parse M-Bus header information
static MBusHeader parseHeaderInfo(const uint8_t *frame, int length, int &index) {
    MBusHeader header = {};

    uint8_t len = frame[1];      // Length from C to end of payload, the length must be repeated in the next byte
    if (len != frame[2]) {
        Serial.print("Length mismatch: ");
        Serial.print(len);
        Serial.print(" != ");
        Serial.println(frame[2]);
        return header;
    }
    uint8_t control = frame[4];  // Steuerbyte (C-Field)
    uint8_t address = frame[5];  // Adresse (A-Field)
    uint8_t CI = frame[6];       // CI-Field (Control Information)
    if (CI != 0x72) {
    Serial.print("Unexpected CI field: 0x");
        Serial.println(CI, HEX);
    }
    // Read header (after CI follow ID, manufacturer, version, medium, access, status, optional signature)

    if (len >= 3 + 9) {
        // 9 Bytes Header (ohne Signatur)
        header.id = DecodeTypeA(frame + index);  // 4 Bytes ID (BCD)
        index += 4;
        header.manufacturer = manufacturerInfoFromCode(frame[index] | (frame[index+1] << 8));
        index += 2;
        header.version = frame[index++];
        header.medium = frame[index++];
        header.accessNo = frame[index++];
        header.status = frame[index++];
        if (len >= 3 + 11) {
            header.signature = frame[index] | (frame[index+1] << 8);
            index += 2;
        }
    }
    
    return header;
}

MBusData parseMBusData(const uint8_t *frame, int length, int &index) {
    MBusData data = {""};

    while (index < length - 2) {  // parse all data before checksum byte and Endbyte
        uint8_t DIF = frame[index++];
        if (DIF == 0x00 || DIF == 0x0F) {
            // no further data (0x0F may indicate filler)
            break;
        }

        uint8_t dataLen = 0;
        bool dataIsBCD = false;
        uint8_t dif_nibble = DIF & 0x0F;
        switch (dif_nibble) {
            case 0x00: dataLen = 0; break;
            case 0x01: dataLen = 1; break;
            case 0x02: dataLen = 2; break;
            case 0x03: dataLen = 3; break;
            case 0x04: dataLen = 4; break;
            case 0x05: dataLen = 4; break;
            case 0x06: dataLen = 6; break;
            case 0x07: dataLen = 8; break;
            case 0x08: dataLen = 0; break;
            case 0x09: dataLen = 2; dataIsBCD = true; break;
            case 0x0A: dataLen = 4; dataIsBCD = true; break;
            case 0x0B: dataLen = 6; dataIsBCD = true; break;
            case 0x0C: dataLen = 8; dataIsBCD = true; break;
            default: /* 0x0D-0x0F sind Sonderfälle, nicht behandelt */ break;
        }

        // Read VIF (Value Information Field)
        if (index >= length - 2) break;
        uint8_t VIF = frame[index++];
        uint8_t VIFE = 0;
        if (vifHasVife(VIF)) {
            // Extended VIF (e.g. manufacturer specific or special)
            if (index < length - 2) {
                VIFE = frame[index++];
            }
        }

        // Read data bytes
        uint8_t dataBytes[8];
        if (dataLen > 0) {
            if (index + dataLen > length - 2) {
                Serial.println("Data bytes missing/insufficient.");
                break;
            }
            for (uint8_t i = 0; i < dataLen; ++i) {
                dataBytes[i] = frame[index + i];
            }
            index += dataLen;
        }

        // Interpret record based on DIF, VIF and VIFE, 
        switch (DIF) {
            case 0x01 : { // Error code
                if (VIF == 0xFD && VIFE == 0x17) {
                    data.status = StatusByteToString(DecodeTypeD(dataBytes, 1));
                }
                else
                    if (debug)
                        Serial.println("Unknown VIF / VIFE for error code. Was " + String(VIF, HEX) + " / " + String(VIFE, HEX) + ", but only 0xFD / 0x17 is valid.");
                break;
            }
            case 0x02 : { // Temperatures and days in operation
                switch (VIF) {
                    case 0x5B : { // Forward flow temperature
                        data.forwardFlowTemperature = MBusIntValue(static_cast<int32_t>(DecodeTypeB(dataBytes, 2)), "°C");
                        break;
                    }
                    case 0x5F : { // Return flow temperature
                        data.returnFlowTemperature = MBusIntValue(static_cast<int32_t>(DecodeTypeB(dataBytes, 2)), "°C");
                        break;
                    }
                    case 0x61 : { // Temperature difference
                        data.temperatureDifference = MBusDoubleValue(static_cast<double>(DecodeTypeB(dataBytes, 2) * 0.01), "°C");
                        break;
                    }
                    case 0x23 : { // Days in operation
                        data.daysInOperation = MBusIntValue(static_cast<int32_t>(DecodeTypeB(dataBytes, 2)), "days");
                        break;
                    }
                    default :
                        if (debug)
                        {
                            Serial.print("Unknown VIF: 0x");
                            Serial.print(VIF, HEX);
                            Serial.println(" in DIF 0x02");
                        }
                }
                break;
            }
            case 0x04 : { // Current Values
                switch (VIF) {
                    case 0x78 : {  // device ID
                        data.deviceId = DecodeTypeA(dataBytes);
                        break;
                    }
                    case 0x06 : { // Total Heat Energy
                        data.totalHeatEnergy = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "MWh");
                        break;
                    }
                    case 0x0E : { // Current value
                        data.currentValue = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "GJ");
                        break;
                    }
                    case 0x86 : {
                        switch (VIFE) {
                            case 0x3D : { // Heatmeter heat
                                data.heatmeterHeat = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "MMBTU");
                                break;
                            }
                        }
                        break;
                    }
                    case 0xDB : {
                        switch (VIFE) {
                            case 0x0D : { // Heat/Coolingmeter heat
                                data.heatCoolingmeterHeat = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "Gcal");
                                break;
                            }
                        }
                        break;
                    }
                    case 0x13 : { // Total Volume
                        data.totalVolume = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "m³");
                        break;
                    }
                    case 0x2B : {
                        data.powerCurrentValue = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "kW");
                        break;
                    }
                    case 0x3B : {
                        data.flowCurrentValue = MBusIntValue(DecodeTypeB(dataBytes, 4), "l/h");
                        break;
                    }
                    case 0x6D : { // Current date and time
                        data.currentDateAndTime = DecodeTypeF(dataBytes);
                        break;
                    }
                    default :
                        if (debug)
                        {
                            Serial.print("Unknown VIF: 0x");
                            Serial.print(VIF, HEX);
                            Serial.println(" in DIF 0x04");
                        }
                }
                break;
            }
            case 0x14 : { // Maximum Values
                switch (VIF) {
                    case 0x2B : {
                        data.powerMaximumValue = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "kW");
                        break;
                    }
                    case 0x3B : {
                        data.flowMaximumValue = MBusIntValue(DecodeTypeB(dataBytes, 2), "l/h");
                        break;
                    }
                    default :
                        if (debug)
                        {
                            Serial.print("Unknown VIF: 0x");
                            Serial.print(VIF, HEX);
                            Serial.println(" in DIF 0x14");
                        }
                }
                break;
            }
            case 0x03 : {
                if (debug) {
                    Serial.println("DIF 0x03 (Device type) not implemented yet -> ignoring");
                    Serial.println("Check https://www.molline.de/fileadmin/content/content/Downloads/Produkte/02_W%C3%A4rmez%C3%A4hler/01_Kompaktz%C3%A4hler/01_WingStar_C3_Familie/M-Bus_Protokoll_Ultramess_C3_WingStar_C3.pdf for more details.");
                }
                break;
            }
  
            case 0x44 :
            case 0x42 : {
                if (debug) {
                    Serial.println("DIF 0x44 / 0x42 (Last billing date period data) not implemented yet -> ignoring");
                    Serial.println("Check https://www.molline.de/fileadmin/content/content/Downloads/Produkte/02_W%C3%A4rmez%C3%A4hler/01_Kompaktz%C3%A4hler/01_WingStar_C3_Familie/M-Bus_Protokoll_Ultramess_C3_WingStar_C3.pdf for more details.");
                }
                break;
            }

            case 0x84 :
            case 0xC4 : {
                if (debug) {
                    Serial.println("DIF 0x84 / 0xC4 (Tariff register and pulse counter) not implemented yet -> ignoring");
                    Serial.println("Check https://www.molline.de/fileadmin/content/content/Downloads/Produkte/02_W%C3%A4rmez%C3%A4hler/01_Kompaktz%C3%A4hler/01_WingStar_C3_Familie/M-Bus_Protokoll_Ultramess_C3_WingStar_C3.pdf for more details.");
                }
                break;
            }
            default:
                if (debug) {
                    Serial.print("Unknown DIF: 0x");
                    Serial.println(DIF, HEX);
                }
                break;
        }
    }

    return data;
}

MBusParsingResult parseMBusFrame(const uint8_t *frame, int length) {
    if (length < 5) {
    Serial.println("Invalid frame (too short).");
        return {};
    }
    // Check if the long frame identifiers can be found at the correct positions
    if (frame[0] != 0x68 || frame[3] != 0x68) {
    Serial.println("No valid M-Bus frame received.");
        return {};
    }

    int index = 7;
    MBusHeader header = parseHeaderInfo(frame, length, index);
    MBusData data = parseMBusData(frame, length, index);

    return MBusParsingResult{ header, data };
}


