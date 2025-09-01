#include "MBusParser.h"

// This code is parsing M-Bus frames from the WDV Molliné Wingstar meter.
// The documentation of the WDV Molliné Wingstar meter can be found here:
// https://www.molline.de/fileadmin/content/content/Downloads/Produkte/02_W%C3%A4rmez%C3%A4hler/01_Kompaktz%C3%A4hler/01_WingStar_C3_Familie/M-Bus_Protokoll_Ultramess_C3_WingStar_C3.pdf

// Static member variable definition
bool MBusParser::debug = false;

// List of VIFs that have VIFE (Value Information Field Extension)
const uint8_t MBusParser::VIFs_WITH_VIFE[] = {
    0x86,  // Extended VIF for MMBTU
    0xFB,  // Extended VIF for Gcal
    0xFD,  // Extended VIF for Error code, Device type, Pulse Counter
};

// Helper function to check if a VIF has a VIFE
bool MBusParser::vifHasVife(uint8_t vif) {
    for (size_t i = 0; i < sizeof(VIFs_WITH_VIFE); ++i) {
        if (vif == VIFs_WITH_VIFE[i]) {
            return true;
        }
    }
    return false;
}

const ManufacturerCodeName MBusParser::manufacturerTable[] = {
    // Source: https://www.m-bus.de/man.html

    {"ABB", "ABB AB, P.O. Box 1005, SE-61129 Nyköping, Nyköping,Sweden"},
    {"ACE", "Actaris (Elektrizität)"},
    {"ACG", "Actaris (Gas)"},
    {"ACW", "Actaris (Wasser und Wärme)"},
    {"AEG", "AEG"},
    {"AEL", "Kohler, Türkei"},
    {"AEM", "S.C. AEM S.A. Romania"},
    {"AMP", "Ampy Automation Digilog Ltd"},
    {"AMT", "Aquametro"},
    {"APS", "Apsis Kontrol Sistemleri, Türkei"},
    {"BEC", "Berg Energiekontrollsysteme GmbH"},
    {"BER", "Bernina Electronic AG"},
    {"BSE", "Basari Elektronik A.S., Türkei"},
    {"BST", "BESTAS Elektronik Optik, Türkei"},
    {"CBI", "Circuit Breaker Industries, Südafrika"},
    {"CLO", "Clorius Raab Karcher Energie Service A/S"},
    {"CON", "Conlog"},
    {"CZM", "Cazzaniga S.p.A."},
    {"DAN", "Danubia"},
    {"DFS", "Danfoss A/S"},
    {"DME", "DIEHL Metering, Industriestrasse 13, 91522 Ansbach, Germany"},
    {"DZG", "Deutsche Zählergesellschaft"},
    {"DWZ", "Lorenz GmbH & Co.KG"},
    {"EDM", "EDMI Pty.Ltd."},
    {"EFE", "Engelmann Sensor GmbH"},
    {"EKT", "PA KVANT J.S., Russland"},
    {"ELM", "Elektromed Elektronik Ltd, Türkei"},
    {"ELS", "ELSTER Produktion GmbH"},
    {"EMH", "EMH Elektrizitätszähler GmbH & CO KG"},
    {"EMU", "EMU Elektronik AG"},
    {"EMO", "Enermet"},
    {"END", "ENDYS GmbH"},
    {"ENP", "Kiev Polytechnical Scientific Research"},
    {"ENT", "ENTES Elektronik, Türkei"},
    {"ERL", "Erelsan Elektrik ve Elektronik, Türkei"},
    {"ESM", "Starion Elektrik ve Elektronik, Türkei"},
    {"EUR", "Eurometers Ltd"},
    {"EWT", "Elin Wasserwerkstechnik"},
    {"FED", "Federal Elektrik, Türkei"},
    {"FML", "Siemens Measurements Ltd.( Formerly FML Ltd.)"},
    {"GBJ", "Grundfoss A/S"},
    {"GEC", "GEC Meters Ltd."},
    {"GSP", "Ingenieurbuero Gasperowicz"},
    {"GWF", "Gas- u. Wassermessfabrik Luzern"},
    {"HEG", "Hamburger Elektronik Gesellschaft"},
    {"HEL", "Heliowatt"},
    {"HRZ", "HERZ Messtechnik GmbH"},
    {"HTC", "Horstmann Timers and Controls Ltd."},
    {"HYD", "Hydrometer GmbH"},
    {"ICM", "Intracom, Griechenland"},
    {"IDE", "IMIT S.p.A."},
    {"INV", "Invensys Metering Systems AG"},
    {"ISK", "Iskraemeco, Slovenia"},
    {"IST", "ista SE"},
    {"ITR", "Itron"},
    {"IWK", "IWK Regler und Kompensatoren GmbH"},
    {"KAM", "Kamstrup Energie A/S"},
    {"KHL", "Kohler, Türkei"},
    {"KKE", "KK-Electronic A/S"},
    {"KNX", "KONNEX-based users (Siemens Regensburg)"},
    {"KRO", "Kromschröder"},
    {"KST", "Kundo SystemTechnik GmbH"},
    {"LEM", "LEM HEME Ltd., UK"},
    {"LGB", "Landis & Gyr Energy Management (UK) Ltd."},
    {"LGD", "Landis & Gyr Deutschland"},
    {"LGZ", "Landis & Gyr Zug"},
    {"LHA", "Atlantic Meters, Südafrika"},
    {"LML", "LUMEL, Polen"},
    {"LSE", "Landis & Staefa electronic"},
    {"LSP", "Landis & Staefa production"},
    {"LUG", "Landis & Staefa"},
    {"LSZ", "Siemens Building Technologies"},
    {"MAD", "Maddalena S.r.I., Italien"},
    {"MEI", "H. Meinecke AG (jetzt Invensys Metering Systems AG)"},
    {"MKS", "MAK-SAY Elektrik Elektronik, Türkei"},
    {"MNS", "MANAS Elektronik, Türkei"},
    {"MPS", "Multiprocessor Systems Ltd, Bulgarien"},
    {"MTC", "Metering Technology Corporation, USA"},
    {"NIS", "Nisko Industries Israel"},
    {"NMS", "Nisko Advanced Metering Solutions Israel"},
    {"NRM", "Norm Elektronik, Türkei"},
    {"ONR", "ONUR Elektroteknik, Türkei"},
    {"PAD", "PadMess GmbH"},
    {"PMG", "Spanner-Pollux GmbH (jetzt Invensys Metering Systems AG)"},
    {"PRI", "Polymeters Response International Ltd."},
    {"RAS", "Hydrometer GmbH"},
    {"REL", "Relay GmbH"},
    {"RKE", "ista SE"},
    {"SAP", "Sappel"},
    {"SCH", "Schnitzel GmbH"},
    {"SEN", "Sensus GmbH"},
    {"SMC", " "},
    {"SME", "Siame, Tunesien"},
    {"SML", "Siemens Measurements Ltd."},
    {"SIE", "Siemens AG"},
    {"SLB", "Schlumberger Industries Ltd."},
    {"SON", "Sontex SA"},
    {"SOF", "softflow.de GmbH"},
    {"SPL", "Sappel"},
    {"SPX", "Spanner Pollux GmbH (jetzt Invensys Metering Systems AG)"},
    {"SVM", "AB Svensk Värmemätning SVM"},
    {"TCH", "Techem Service AG"},
    {"TIP", "TIP Thüringer Industrie Produkte GmbH"},
    {"UAG", "Uher"},
    {"UGI", "United Gas Industries"},
    {"VES", "ista SE"},
    {"VPI", "Van Putten Instruments B.V."},
    {"WMO", "Westermo Teleindustri AB, Schweden"},
    {"YTE", "Yuksek Teknoloji, Türkei"},
    {"ZAG", "Zellwerg Uster AG"},
    {"ZAP", "Zaptronix"},
    {"ZIV", "ZIV Aplicaciones y Tecnologia, S.A."},
};

ManufacturerInfo MBusParser::manufacturerInfoFromCode(uint16_t manCode) {
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
String MBusParser::mediumCodeToString(uint8_t medium)
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
String MBusParser::statusByteToString(uint8_t status) {
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
String MBusParser::DecodeTypeA_BCD(const uint8_t* d) {
    if (debug) {
        Serial.print("Decoding Type A for ");
        for (int i = 0; i < 4; ++i) {
            Serial.print(d[i], HEX);
            Serial.print(" ");
        }
        Serial.println();
    }
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

// ---------- Type A (32-bit little-endian binary integer) ----------
// Decodes 4 bytes of little-endian binary integer.
uint32_t MBusParser::DecodeTypeA_UInt32(const uint8_t* b4) {
    if (debug) {
        Serial.print("Decoding Type A for ");
        for (int i = 0; i < 4; ++i) {
            Serial.print(b4[i], HEX);
            Serial.print(" ");
        }
        Serial.println();
    }
    return read_u_le(b4, 4);
}

// ---------- Type B (signed binary, two's complement) ----------
/* nBytes must be 1..8 */
int32_t MBusParser::DecodeTypeB(const uint8_t* d, size_t size) {
  return read_s_le(d, size);
}

// ---------- Type C (unsigned binary) ----------
/* nBytes must be 1..8 */
bool MBusParser::DecodeTypeC(const uint8_t* d, size_t nBytes, uint64_t& value) {
  if (nBytes == 0 || nBytes > 8) return false;
  value = read_u_le(d, nBytes);
  return true;
}

// ---------- Type D (bit field) ----------
/* Returns the bitfield as an unsigned integer (LSB = bit 0). */
uint64_t MBusParser::DecodeTypeD(const uint8_t* d, size_t nBytes) {
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
MBusDateTime MBusParser::DecodeTypeF(const uint8_t* d) {
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
MBusHeader MBusParser::parseHeaderInfo(const uint8_t *frame, int length, int &index) {
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
        Serial.print("Parsing header ...");
        header.id = DecodeTypeA_BCD(frame + index);  // 4 Bytes ID (BCD)
        index += 4;
        header.manufacturer = MBusParser::manufacturerInfoFromCode(frame[index] | (frame[index+1] << 8));
        index += 2;
        header.version = frame[index++];
        header.medium = frame[index++];
        header.accessNo = frame[index++];
        header.status = frame[index++];
        if (len >= 3 + 11) {
            header.signature = frame[index] | (frame[index+1] << 8);
            index += 2;
        }
        Serial.println(" Done");
    }
    
    return header;
}

MBusData MBusParser::parseMBusData(const uint8_t *frame, int length, int &index) {
    MBusData data = {};
    Serial.print("Parsing Data ...");
    if (debug)
    {
        Serial.println("Frame data:");
        for (int i = 0; i < length; ++i) {
            Serial.print(frame[i], HEX);
            Serial.print(" ");
        }
        Serial.println();
    }

    while (index < length - 2) {  // parse all data before checksum byte and Endbyte
        uint8_t DIF = frame[index++];
        if (DIF == 0x00 || DIF == 0x0F) {
            // no further data (0x0F may indicate filler)
            break;
        }

        // The last nibble of DIF defines the data length
        uint8_t dataLen = DIF & 0x0F;

        // Read VIF (Value Information Field)
        if (index >= length - 2) break;
        uint8_t VIF = frame[index++];
        uint8_t VIFE = 0;
        if (MBusParser::vifHasVife(VIF)) {
            if (index < length - 2) {
                VIFE = frame[index++];
            }
        }

        // Read data bytes
        uint8_t dataBytes[8];
        if (!MBusParser::ExtractDataFromFrame(dataLen, index, length, dataBytes, frame)) {
            break;
        }

        // Interpret record based on DIF, VIF and VIFE, 
        switch (DIF) {
            case 0x01 : { // Error code
                if (VIF == 0xFD && VIFE == 0x17) {
                    if (debug) Serial.println("Parsing error code...");
                    data.status = statusByteToString(DecodeTypeD(dataBytes, 1));
                }
                else
                    if (debug)
                        Serial.println("Unknown VIF / VIFE for error code. Was " + String(VIF, HEX) + " / " + String(VIFE, HEX) + ", but only 0xFD / 0x17 is valid.");
                break;
            }
            case 0x02 : { // Temperatures and days in operation
                switch (VIF) {
                    case 0x5B : { // Forward flow temperature
                        if (debug) Serial.println("Parsing forward flow temperature...");
                        data.forwardFlowTemperature = MBusIntValue(static_cast<int32_t>(DecodeTypeB(dataBytes, 2)), "°C");
                        break;
                    }
                    case 0x5F : { // Return flow temperature
                        if (debug) Serial.println("Parsing return flow temperature...");
                        data.returnFlowTemperature = MBusIntValue(static_cast<int32_t>(DecodeTypeB(dataBytes, 2)), "°C");
                        break;
                    }
                    case 0x61 : { // Temperature difference
                        if (debug) Serial.println("Parsing temperature difference...");
                        data.temperatureDifference = MBusDoubleValue(static_cast<double>(DecodeTypeB(dataBytes, 2) * 0.01), "°C");
                        break;
                    }
                    case 0x23 : { // Days in operation
                        if (debug) Serial.println("Parsing days in operation...");
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
                        if (debug) {Serial.println(); Serial.println("Parsing device ID...");}
                        data.deviceId = DecodeTypeA_UInt32(dataBytes);
                        break;
                    }
                    case 0x06 : { // Total Heat Energy
                        if (debug) Serial.println("Parsing total heat energy...");
                        data.totalHeatEnergy = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "MWh");
                        break;
                    }
                    case 0x0E : { // Current value
                        if (debug) Serial.println("Parsing current value...");
                        data.currentValue = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "GJ");
                        break;
                    }
                    case 0x86 : {
                        switch (VIFE) {
                            case 0x3D : { // Heatmeter heat
                                if (debug) Serial.println("Parsing heatmeter heat...");
                                data.heatmeterHeat = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "MMBTU");
                                break;
                            }
                        }
                        break;
                    }
                    case 0xDB : {
                        switch (VIFE) {
                            case 0x0D : { // Heat/Coolingmeter heat
                                if (debug) Serial.println("Parsing heat/coolingmeter heat...");
                                data.heatCoolingmeterHeat = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "Gcal");
                                break;
                            }
                        }
                        break;
                    }
                    case 0x13 : { // Total Volume
                        if (debug) Serial.println("Parsing total volume...");
                        data.totalVolume = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "m³");
                        break;
                    }
                    case 0x2B : {
                        if (debug) Serial.println("Parsing power current value...");
                        data.powerCurrentValue = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "kW");
                        break;
                    }
                    case 0x3B : {
                        if (debug) Serial.println("Parsing flow current value...");
                        data.flowCurrentValue = MBusIntValue(DecodeTypeB(dataBytes, 4), "l/h");
                        break;
                    }
                    case 0x6D : { // Current date and time
                        if (debug) Serial.println("Parsing current date and time...");
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
                        if (debug) Serial.println("Parsing power maximum value...");
                        data.powerMaximumValue = MBusDoubleValue(DecodeTypeB(dataBytes, 4) * 0.001, "kW");
                        break;
                    }
                    case 0x3B : {
                        if (debug) Serial.println("Parsing flow maximum value...");
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
    Serial.println(" Done");
    return data;
}

bool MBusParser::ExtractDataFromFrame(uint8_t dataLen, int &index, int length, uint8_t dataBytes[8], const uint8_t *frame)
{
    if (dataLen <= 0)
    {
        Serial.println("Invalid data length.");
        return false;
    }

    if (index + dataLen > length - 2)
    {
        Serial.println("Data bytes missing/insufficient.");
        return false;
    }
    for (uint8_t i = 0; i < dataLen; ++i)
    {
        dataBytes[i] = frame[index + i];
    }
    index += dataLen;
    return true;
}

MBusParsingResult MBusParser::parseMBusFrame(const uint8_t *frame, int length) {
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


