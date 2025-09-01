#ifndef MBUSCOMM_H
#define MBUSCOMM_H

#include <Arduino.h>

bool MBusInit(int rxPin, int txPin, long baud = 2400, bool debug = false);

// Sends 504×0x55 with 8N1 and switches back to 8E1
void MBusSendPreamble();

bool MBusSendWakeUp(uint8_t primaryAddress = 0);
bool WaitForAck(uint8_t frame[5]);
bool MBusSendRequest(uint8_t primaryAddress = 0);
int  MBusReadResponse(uint8_t *buffer, int maxLen);

#endif
