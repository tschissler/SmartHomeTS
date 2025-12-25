#pragma once
#include <Arduino.h>
#include <map>

class WifiLib {
public:
    WifiLib(const String& wifiPasswords);
    void scanAndSelectNetwork();
    void connect();
    bool connect(uint32_t timeoutMs);
    String getSSID() const;
    String getPassword() const;
    String getLocalIP() const;
    String getBSSID() const;
private:
    String ssid;
    String password;
    String passwords;
    uint8_t bssid[6];
    bool bssidSet;
    void parseWifis(std::map<String, String> &knownWifis);
};
