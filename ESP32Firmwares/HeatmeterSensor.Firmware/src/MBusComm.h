#ifndef MBUSCOMM_H
#define MBUSCOMM_H

#include <Arduino.h>

class MBusComm {
private:
    int rxPin;
    int txPin;
    long baud;
    
    // Private helper method
    bool waitForAck(uint8_t frame[5]);
    
public:
    // Constructor
    MBusComm();
    
    // Initialization
    bool init(int rxPin, int txPin, long baud = 2400, bool debugMode = false);
    
    // Communication methods
    void sendPreamble();
    bool sendWakeUp(uint8_t primaryAddress = 0);
    bool sendRequest(uint8_t primaryAddress = 0);
    int readResponse(uint8_t *buffer, int maxLen);
};

#endif
