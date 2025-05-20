#include "ESP32Helpers.h"
#include <esp_mac.h> 

String ESP32Helpers::getChipId() { 
uint8_t mac[6];
  esp_read_mac(mac, ESP_MAC_WIFI_STA);

  String chipId = "";
  for (int i = 0; i < 6; ++i) {
    if (mac[i] < 0x10) chipId += "0";  // Add leading zero if needed
    chipId += String(mac[i], HEX);
  }
    return chipId; 
}