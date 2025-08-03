#include <Arduino.h>
#include "temperature_display.h"

// Create display instance
TemperatureDisplay display;

// Callback functions for display events
void onTemperatureChanged(float temperature, Room room) {
  Serial.printf("Temperature changed to %.1f°C in %s\n", 
                temperature, display.roomToString(room));
  // Here you could send the temperature change to a server, MQTT, etc.
}

void onRoomChanged(Room oldRoom, Room newRoom) {
  Serial.printf("Room changed from %s to %s\n", 
                display.roomToString(oldRoom), display.roomToString(newRoom));
  // Here you could load room-specific settings, update server, etc.
}

void setup()
{
  Serial.begin(115200);
  Serial.println("------------------  Temperature Display Firmware  ------------------");
  
  // Initialize display
  if (!display.init()) {
    Serial.println("Failed to initialize display");
    return;
  }
  
  if (!display.begin()) {
    Serial.println("Failed to start display");
    return;
  }
  
  // Setup UI
  display.setupUI();
  
  // Set callback functions
  display.setTemperatureChangeCallback(onTemperatureChanged);
  display.setRoomChangeCallback(onRoomChanged);
  
  Serial.println("Initialization complete");
}

void loop()
{
  // Update display
  display.update();
  
  // Simulate temperature sensor readings
  display.simulateTemperatureSensor();
}
