#ifndef MBUSPARSER_H
#define MBUSPARSER_H

#include <Arduino.h>

// Forward declaration of MBusHeader struct
struct MBusHeader;
struct ManufacturerInfo;
struct ManufacturerCodeName;

extern bool debug;

void parseMBusFrame(const uint8_t *frame, int length);

#endif
