#include "WifiLib.h"
#include <WiFi.h>

WifiLib::WifiLib(const String& wifiPasswords) : passwords(wifiPasswords), ssid(""), password("") {}

void WifiLib::scanAndSelectNetwork() {
    Serial.println("Scanning for WiFi networks...");
    std::map<String, String> knownWifis;
    parseWifis(knownWifis);

    if (knownWifis.size() == 0) {
        Serial.println("No known WiFi networks defined, will not connect to Wifi.");
        return;
    }

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
        if (WiFi.RSSI(i) > maxRSSI && knownWifis.count(WiFi.SSID(i)) > 0) {
            maxRSSI = WiFi.RSSI(i);
            maxRSSIIndex = i;
        }
    }

    if (maxRSSIIndex == -1) {
        Serial.println("No WiFi network found that is contained in the list of known networks.");
        Serial.println("Please check your environment variable 'WIFI_PASSWORDS'.");
        Serial.println("Defined networks are:");
        for (const auto& pair : knownWifis) {
            Serial.print(" - ");
            Serial.println(pair.first);
        }

        ssid = "";
        password = "";
        return;
    } else {
        ssid = WiFi.SSID(maxRSSIIndex);
        password = knownWifis[ssid];
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
        Serial.println("Could not connect to Wifi " + ssid + " - password might be incorrect, retrying...");
    }
    Serial.println("Connected to WiFi");
    Serial.print("IP Address: ");
    Serial.println(WiFi.localIP());
}

void WifiLib::parseWifis(std::map<String, String> &knownWifis) {
    knownWifis.clear();
    int start = 0;
    while (start < passwords.length()) {
        int end = passwords.indexOf('|', start);
        if (end == -1) end = passwords.length();
        String entry = passwords.substring(start, end);
        int sep = entry.indexOf(';');
        if (sep == -1 || sep == 0 || sep == entry.length() - 1) {
            Serial.println("Error: Invalid WiFi password format. Each entry must be 'SSID;password'.");
            Serial.println("Offending entry: " + entry);
        } else {
            knownWifis[entry.substring(0, sep)] = entry.substring(sep + 1);
        }
        start = end + 1;
    }
}

String WifiLib::getSSID() const { return ssid; }
String WifiLib::getPassword() const { return password; }
String WifiLib::getLocalIP() const { return WiFi.localIP().toString(); }
