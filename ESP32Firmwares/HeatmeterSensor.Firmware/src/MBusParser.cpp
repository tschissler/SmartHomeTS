#include "MBusParser.h"

// This code is parsing M-Bus frames from the WDV Molliné Wingstar meter.
// The documentation of the WDV Molliné Wingstar meter can be found here:
// https://www.molline.de/fileadmin/content/content/Downloads/Produkte/02_W%C3%A4rmez%C3%A4hler/01_Kompaktz%C3%A4hler/01_WingStar_C3_Familie/M-Bus_Protokoll_Ultramess_C3_WingStar_C3.pdf


// Struct to hold manufacturer info
struct ManufacturerInfo {
    String code;
    String name;
};

// Struct to hold M-Bus header data
struct MBusHeader {
    uint32_t id;
    ManufacturerInfo manufacturer;
    uint8_t version;
    uint8_t medium;
    uint8_t accessNo;
    uint8_t status;
    uint16_t signature;
};

struct MBusData {
    uint32_t deviceId;

};

// Helper: convert the 2-byte manufacturer code (M-Bus) into a 3-letter ASCII manufacturer ID and name.
struct ManufacturerCodeName {
    const char* code;
    const char* name;
};

static const ManufacturerCodeName manufacturerTable[] = {
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
String mediumCodeToString(uint8_t medium)
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
String statusByteToString(uint8_t status) {
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

// Helper: convert BCD encoded bytes (e.g. for meter ID) into an integer.
unsigned long bcdToUInt(const uint8_t *data, int length) {
    unsigned long value = 0;
    unsigned long place = 1;
    for (int i = 0; i < length; ++i) {
        uint8_t byte = data[i];
        uint8_t lowNibble = byte & 0x0F;
        uint8_t highNibble = (byte >> 4) & 0x0F;
    // lower digit
        value += lowNibble * place;
        place *= 10;
    // higher digit
        value += highNibble * place;
        place *= 10;
    }
    return value;
}

void PrintHeaderInfo(MBusHeader &header)
{
    Serial.println("M-Bus Header");
    Serial.println("-------------------------------------------");
    Serial.println("Identification number: " + String(header.id));
    Serial.println("Manufacturer: " + String(header.manufacturer.name) + " (" + String(header.manufacturer.code) + ")");
    Serial.println("Meter version: " + String(header.version));
    Serial.print("Medium: 0x");
    Serial.print(header.medium, HEX);
    Serial.println(" (" + String(mediumCodeToString(header.medium)) + ")");
    Serial.println("Access number: " + String(header.accessNo));
    Serial.print("Status: 0x");
    Serial.print(header.status, HEX);
    Serial.println(" => " + statusByteToString(header.status));
    Serial.print("Signature: 0x");
    Serial.println(header.signature, HEX);
}

void parseMBusFrame(const uint8_t *frame, int length) {
    MBusHeader header = {0};
    MBusData data = {0};

    if (length < 5) {
    Serial.println("Invalid frame (too short).");
        return;
    }
    // Check if the long frame identifiers can be found at the correct positions
    if (frame[0] != 0x68 || frame[3] != 0x68) {
    Serial.println("No valid M-Bus frame received.");
        return;
    }
    uint8_t len = frame[1];      // Length from C to end of payload, the length must be repeated in the next byte
    if (len != frame[2]) {
        Serial.print("Length mismatch: ");
        Serial.print(len);
        Serial.print(" != ");
        Serial.println(frame[2]);
        return;
    }
    uint8_t control = frame[4];  // Steuerbyte (C-Field)
    uint8_t address = frame[5];  // Adresse (A-Field)
    uint8_t CI = frame[6];       // CI-Field (Control Information)
    if (CI != 0x72) {
    Serial.print("Unexpected CI field: 0x");
        Serial.println(CI, HEX);
    }
    // Read header (after CI follow ID, manufacturer, version, medium, access, status, optional signature)
    int index = 7;
    
    if (len >= 3 + 9) {
        // 9 Bytes Header (ohne Signatur)
        header.id = bcdToUInt(frame + index, 4);  // 4 Bytes ID (BCD)
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
    
    // Output header info
    PrintHeaderInfo(header);

    Serial.println();
    Serial.println("M-Bus Data Records");
    Serial.println("-------------------------------------------");
    
    while (index < length - 2) {  // bis vor Prüfsummen-Byte und Endbyte
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
    // Process DIFE (storage number / tariff if present)
        uint8_t storageNo = 0;
        if (DIF & 0x80) {
            uint8_t DIFE = frame[index++];
            storageNo = DIFE & 0x0F;
            // Ignore further DIFE bits (tariff etc.) in this example
        }
    // Read VIF (Value Information Field)
        if (index >= length - 2) break;
        uint8_t VIF = frame[index++];
        uint8_t VIFE = 0;
        if (VIF == 0xFD || VIF == 0xFB) {
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

    // Interpret record based on VIF
    if (VIF == 0x78) {  // device ID (manufacturer number)
            unsigned long idVal;
            if (dataIsBCD) {
                idVal = bcdToUInt(dataBytes, dataLen / 2);
            } else {
                // Falls doch als Binärwert gesendet (unwahrscheinlich), alternativ:
                idVal = 0;
                for (int i = 0; i < dataLen; ++i) {
                    idVal |= ((unsigned long)dataBytes[i] << (8 * i));
                }
            }
            Serial.print("Meter ID: ");
            Serial.println(idVal);
            idPrinted = true;
            continue;
        }
        if (VIF == 0x06 || VIF == 0x0E || (VIF == 0x3D && VIFE == 0x86) || (VIF == 0xFB && VIFE == 0x0D)) {
            // Energy (heat/cold)
            unsigned long raw = 0;
            for (int i = 0; i < dataLen; ++i) {
                raw |= ((unsigned long)dataBytes[i] << (8 * i));
            }
            double value = raw;
            String unit = "";
            if (VIF == 0x06) {
                value = raw * 0.001;  // in MWh
                unit = "MWh";
            } else if (VIF == 0x0E) {
                value = raw * 0.001;  // in GJ
                unit = "GJ";
            } else if (VIF == 0x3D && VIFE == 0x86) {
                value = raw * 0.001;  // in MMBTU
                unit = "MMBTU";
            } else if (VIF == 0xFB && VIFE == 0x0D) {
                value = raw * 0.001;  // in Gcal
                unit = "Gcal";
            }
            Serial.print("Thermal energy: ");
            Serial.print(value, 3);
            Serial.print(" ");
            Serial.println(unit);
            continue;
        }
        if (VIF == 0x13) {  // Volumen
            unsigned long raw = 0;
            for (int i = 0; i < dataLen; ++i) {
                raw |= ((unsigned long)dataBytes[i] << (8 * i));
            }
            double value = raw * 0.001;  // m³
            Serial.print("Volume: ");
            Serial.print(value, 3);
            Serial.println(" m³");
            continue;
        }
        if (VIF == 0x3B) {  // flow (l/h)
            unsigned long raw = 0;
            for (int i = 0; i < dataLen; ++i) {
                raw |= ((unsigned long)dataBytes[i] << (8 * i));
            }
            Serial.print("Flow: ");
            Serial.print(raw);
            Serial.println(" l/h");
            continue;
        }
        if (VIF == 0x5B || VIF == 0x5F) {  // temperature (°C)
            int16_t raw = 0;
            for (int i = 0; i < dataLen; ++i) {
                raw |= ((uint16_t)dataBytes[i] << (8 * i));
            }
            String label = (VIF == 0x5B ? "Flow temp." : "Return temp.");
            Serial.print(label + ": ");
            Serial.print(raw);
            Serial.println(" °C");
            continue;
        }
        if (VIF == 0x61) {  // temperature difference (0.01°C)
            int16_t raw = 0;
            for (int i = 0; i < dataLen; ++i) {
                raw |= ((uint16_t)dataBytes[i] << (8 * i));
            }
            double value = raw * 0.01;
            Serial.print("Temp. difference: ");
            Serial.print(value, 2);
            Serial.println(" °C");
            continue;
        }
        if (VIF == 0x23) {  // operating time in days
            uint16_t raw = dataBytes[0] | (dataBytes[1] << 8);
            Serial.print("Operating days: ");
            Serial.print(raw);
            Serial.println(" days");
            continue;
        }
        // Show unknown record (hex values)
        Serial.print("Unknown VIF 0x");
        Serial.print(VIF, HEX);
        if (VIFE != 0) {
            Serial.print(" VIFE 0x");
            Serial.print(VIFE, HEX);
        }
        Serial.print(" Data: ");
        for (int i = 0; i < dataLen; ++i) {
            if (dataBytes[i] < 0x10) Serial.print("0");
            Serial.print(dataBytes[i], HEX);
            Serial.print(" ");
        }
        Serial.println();
    }
    // If the meter ID was not output as a record, output from header:
    if (!idPrinted && header.id != 0) {
        Serial.print("Meter ID: ");
        Serial.println(header.id);
    }
}


