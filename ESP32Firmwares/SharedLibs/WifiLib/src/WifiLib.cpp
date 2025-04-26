#include "WifiLib.h"
#include <WiFi.h>

WifiLib::WifiLib(const String& wifiPasswords) : passwords(wifiPasswords), ssid(""), password("") {}

void WifiLib::scanAndSelectNetwork() {
    Serial.println("Scanning for WiFi networks...");
    int numberOfNetworks = WiFi.scanNetworks();
    Serial.print("Found ");
    Serial.print(numberOfNetworks);
    Serial.println(" networks.");
    for (int i = 0; i < numberOfNetworks; i++) {
        Serial.print(WiFi.SSID(i));
        Serial.print(" (");
        Serial.print(WiFi.RSSI(i));
        Serial.print(") ");
        Serial.println(WiFi.encryptionType(i));
        delay(10);
    }
    int maxRSSI = -1000;
    int maxRSSIIndex = -1;
    for (int i = 0; i < numberOfNetworks; i++) {
        if (WiFi.RSSI(i) > maxRSSI && passwords.indexOf(WiFi.SSID(i)) >= 0) {
            maxRSSI = WiFi.RSSI(i);
            maxRSSIIndex = i;
        }
    }
    if (maxRSSIIndex == -1) {
        Serial.println("No WiFi network found");
        ssid = "";
        password = "";
        return;
    } else {
        ssid = WiFi.SSID(maxRSSIIndex);
        int startIdx = passwords.indexOf(ssid) + ssid.length() + 1;
        int endIdx = passwords.indexOf('|', passwords.indexOf(ssid));
        if (endIdx == -1) endIdx = passwords.length();
        password = passwords.substring(startIdx, endIdx);
        Serial.println("Strongest known WiFi network is " + ssid + " with RSSI " + String(maxRSSI) + " dBm");
    }
}

void WifiLib::connect() {
    while (ssid == "" || password == "") {
        Serial.println("No WiFi network found, retrying...");
        delay(1000);
        scanAndSelectNetwork();
    }
    Serial.print("Connecting to WiFi ");
    Serial.println(ssid);
    WiFi.begin(ssid.c_str(), password.c_str());
    while (WiFi.status() != WL_CONNECTED) {
        delay(1000);
        Serial.println("Connecting to WiFi...");
    }
    Serial.println("Connected to WiFi");
    Serial.print("IP Address: ");
    Serial.println(WiFi.localIP());
}

String WifiLib::getSSID() const { return ssid; }
String WifiLib::getPassword() const { return password; }
String WifiLib::getLocalIP() const { return WiFi.localIP().toString(); }
