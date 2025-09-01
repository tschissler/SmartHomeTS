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

// List of VIFs that have VIFE (Value Information Field Extension)
static const uint8_t VIFs_WITH_VIFE[] = {
    0x86,  // Extended VIF for MMBTU
    0xFB,  // Extended VIF for Gcal
    0xFD,  // Extended VIF for Error code, Device type, Pulse Counter
};

#endif
