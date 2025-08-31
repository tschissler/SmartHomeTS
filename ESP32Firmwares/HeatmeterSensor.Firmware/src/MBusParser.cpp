#include "MBusParser.h"

// Hilfsfunktion: Wandelt den 2-Byte Herstellercode (M-Bus) in einen 3-stelligen ASCII-Code um.
String manufacturerCodeToString(uint16_t manCode) {
    char letters[4];
    letters[0] = ((manCode >> 10) & 0x1F) + 'A' - 1;
    letters[1] = ((manCode >> 5) & 0x1F) + 'A' - 1;
    letters[2] = (manCode & 0x1F) + 'A' - 1;
    letters[3] = '\0';
    return String(letters);
}

// Hilfsfunktion: Wandelt BCD-codierte Bytes (z.B. für Zähler-ID) in einen Integer um.
unsigned long bcdToUInt(const uint8_t *data, int length) {
    unsigned long value = 0;
    unsigned long place = 1;
    for (int i = 0; i < length; ++i) {
        uint8_t byte = data[i];
        uint8_t lowNibble = byte & 0x0F;
        uint8_t highNibble = (byte >> 4) & 0x0F;
        // niedrigwertige Ziffer
        value += lowNibble * place;
        place *= 10;
        // höherwertige Ziffer
        value += highNibble * place;
        place *= 10;
    }
    return value;
}

void parseMBusFrame(const uint8_t *frame, int length) {
    if (length < 5) {
        Serial.println("Ungültiger Frame (zu kurz).");
        return;
    }
    // Prüfen auf gültigen Start
    if (frame[0] != 0x68 || frame[3] != 0x68) {
        Serial.println("Kein gültiger M-Bus Rah­men empfangen.");
        return;
    }
    uint8_t len = frame[1];      // Länge von C bis Ende Nutzdaten
    uint8_t control = frame[4];  // Steuerbyte (C-Field)
    uint8_t address = frame[5];  // Adresse (A-Field)
    uint8_t CI = frame[6];       // CI-Field (Control Information)
    if (CI != 0x72) {
        Serial.print("Unerwartetes CI-Field: 0x");
        Serial.println(CI, HEX);
        // Falls fester Datensatz (CI=0x73) oder andere, hier nicht implementiert.
    }
    // Header auslesen (nach CI folgen ID, Hersteller, Version, Medium, Access, Status, ggf. Signatur)
    int index = 7;
    uint32_t id = 0;
    uint16_t manufacturer = 0;
    uint8_t version = 0;
    uint8_t medium = 0;
    uint8_t accessNo = 0;
    uint8_t status = 0;
    uint16_t signature = 0;
    if (len >= 3 + 9) {
        // 9 Bytes Header (ohne Signatur)
        id = bcdToUInt(frame + index, 4);  // 4 Bytes ID (BCD)
        index += 4;
        manufacturer = frame[index] | (frame[index+1] << 8);
        index += 2;
        version = frame[index++];
        medium = frame[index++];
        accessNo = frame[index++];
        status = frame[index++];
        if (len >= 3 + 11) {
            // 2 Byte Signatur vorhanden (z.B. für Fehlercode oder ähnliches)
            signature = frame[index] | (frame[index+1] << 8);
            index += 2;
        }
    }
    // Header-Infos ausgeben
    Serial.print("Hersteller: ");
    Serial.println(manufacturerCodeToString(manufacturer));
    Serial.print("Medium: 0x");
    Serial.print(medium, HEX);
    Serial.print(" (");
    switch (medium) {
        case 0x02: Serial.print("Elektrizität"); break;
        case 0x03: Serial.print("Gas"); break;
        case 0x04: Serial.print("Wärme"); break;
        case 0x06: Serial.print("Warmwasser"); break;
        case 0x07: Serial.print("Wasser"); break;
        case 0x08: Serial.print("Heizkostenverteiler"); break;
        case 0x0A: Serial.print("Kälte (Outlet)"); break;
        case 0x0B: Serial.print("Kälte (Inlet)"); break;
        case 0x0C: Serial.print("Wärme (Inlet)"); break;
        case 0x0D: Serial.print("Wärme/Kälte komb."); break;
        default: Serial.print("Unbekannt"); break;
    }
    Serial.println(")");
    if (status != 0) {
        Serial.print("Status: 0x");
        Serial.println(status, HEX);
    }
    // Datensätze parsen
    bool idPrinted = false;
    while (index < length - 2) {  // bis vor Prüfsummen-Byte und Endbyte
        uint8_t DIF = frame[index++];
        if (DIF == 0x00 || DIF == 0x0F) {
            // keine weiteren Daten (0x0F kann Füller bedeuten)
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
            case 0x05: dataLen = 4; /* 32-bit Real (wird wie Int behandelt) */ break;
            case 0x06: dataLen = 6; break;
            case 0x07: dataLen = 8; break;
            case 0x08: dataLen = 0; /* Auswahl für Aus­lese, hier nicht relevant */ break;
            case 0x09: dataLen = 2; dataIsBCD = true; break;
            case 0x0A: dataLen = 4; dataIsBCD = true; break;
            case 0x0B: dataLen = 6; dataIsBCD = true; break;
            case 0x0C: dataLen = 8; dataIsBCD = true; break;
            default: /* 0x0D-0x0F sind Sonderfälle, nicht behandelt */ break;
        }
        // DIFE verarbeiten (Speichernummer/Tarif falls vorhanden)
        uint8_t storageNo = 0;
        if (DIF & 0x80) {
            uint8_t DIFE = frame[index++];
            storageNo = DIFE & 0x0F;
            // Wir ignorieren weitere DIFE-Bits (Tarif etc.) in diesem Beispiel
        }
        // VIF auslesen (Value Information Field)
        if (index >= length - 2) break;
        uint8_t VIF = frame[index++];
        uint8_t VIFE = 0;
        if (VIF == 0xFD || VIF == 0xFB) {
            // Erweiterter VIF (z.B. Herstellerspezifisch oder Sonder)
            if (index < length - 2) {
                VIFE = frame[index++];
            }
        }
        // Datenbytes lesen
        uint8_t dataBytes[8];
        if (dataLen > 0) {
            if (index + dataLen > length - 2) {
                Serial.println("Datenbytes fehlen/ungenügend.");
                break;
            }
            for (uint8_t i = 0; i < dataLen; ++i) {
                dataBytes[i] = frame[index + i];
            }
            index += dataLen;
        }
        // Datensatz anhand VIF interpretieren
        if (VIF == 0x78) {  // Geräte-ID (Herstellnr.)
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
            Serial.print("Zähler-ID: ");
            Serial.println(idVal);
            idPrinted = true;
            continue;
        }
        if (VIF == 0x06 || VIF == 0x0E || (VIF == 0x3D && VIFE == 0x86) || (VIF == 0xFB && VIFE == 0x0D)) {
            // Energie (Wärme/Kälte)
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
            Serial.print("Wärmeenergie: ");
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
            Serial.print("Volumen: ");
            Serial.print(value, 3);
            Serial.println(" m³");
            continue;
        }
        if (VIF == 0x3B) {  // Durchfluss (l/h)
            unsigned long raw = 0;
            for (int i = 0; i < dataLen; ++i) {
                raw |= ((unsigned long)dataBytes[i] << (8 * i));
            }
            Serial.print("Durchfluss: ");
            Serial.print(raw);
            Serial.println(" l/h");
            continue;
        }
        if (VIF == 0x5B || VIF == 0x5F) {  // Temperatur (°C)
            int16_t raw = 0;
            for (int i = 0; i < dataLen; ++i) {
                raw |= ((uint16_t)dataBytes[i] << (8 * i));
            }
            String label = (VIF == 0x5B ? "Vorlauf-Temp." : "Rücklauf-Temp.");
            Serial.print(label + ": ");
            Serial.print(raw);
            Serial.println(" °C");
            continue;
        }
        if (VIF == 0x61) {  // Temperaturdifferenz (0,01°C)
            int16_t raw = 0;
            for (int i = 0; i < dataLen; ++i) {
                raw |= ((uint16_t)dataBytes[i] << (8 * i));
            }
            double value = raw * 0.01;
            Serial.print("Temp.-Differenz: ");
            Serial.print(value, 2);
            Serial.println(" °C");
            continue;
        }
        if (VIF == 0x23) {  // Betriebszeit in Tagen
            uint16_t raw = dataBytes[0] | (dataBytes[1] << 8);
            Serial.print("Betriebstage: ");
            Serial.print(raw);
            Serial.println(" Tage");
            continue;
        }
        // Unbekannten Datensatz anzeigen (Hex-Werte)
        Serial.print("Unbekannt VIF 0x");
        Serial.print(VIF, HEX);
        if (VIFE != 0) {
            Serial.print(" VIFE 0x");
            Serial.print(VIFE, HEX);
        }
        Serial.print(" Daten: ");
        for (int i = 0; i < dataLen; ++i) {
            if (dataBytes[i] < 0x10) Serial.print("0");
            Serial.print(dataBytes[i], HEX);
            Serial.print(" ");
        }
        Serial.println();
    }
    // Falls die Zähler-ID nicht als Datensatz ausgegeben wurde, aus dem Header ausgeben:
    if (!idPrinted && id != 0) {
        Serial.print("Zähler-ID: ");
        Serial.println(id);
    }
}
