#ifndef MBUSPARSER_H
#define MBUSPARSER_H

#include <Arduino.h>

void parseMBusFrame(const uint8_t *frame, int length);

#endif
