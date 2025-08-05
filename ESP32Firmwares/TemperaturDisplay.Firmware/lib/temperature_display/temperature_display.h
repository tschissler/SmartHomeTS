#ifndef TEMPERATURE_DISPLAY_H
#define TEMPERATURE_DISPLAY_H

#include <Arduino.h>
#include <esp_display_panel.hpp>
#include <lvgl.h>
#include <time.h>
#include <sys/time.h>
#include "lvgl_v9_port.h"
#include "ui.h"
#include "thermostat_data.h"

// Forward declaration
class ThermostatData;

using namespace esp_panel::drivers;
using namespace esp_panel::board;

// Enum for room selection
enum class Room : int {
    Wohnzimmer,
    Esszimmer,
    Kueche,
    Gaestezimmer,
    Buero,
    Schlafzimmer,
    Bad
};

enum class Status : uint8_t {
    NONE = 0,   
    TRANSFER,         
    ERROR,       
};

// Callback function types for events
typedef void (*TemperatureChangeCallback)(float temperature, Room room);
typedef void (*TemperatureSetCallback)(float temperature, Room room);
typedef void (*RoomChangeCallback)(Room oldRoom, Room newRoom);

class TemperatureDisplay {
private:
    Board* board;
    Room currentRoom;
    float currentTemperature;
    float targetTemperature;
    
    // Thermostat data storage (indexed by Room enum)
    static const int ROOM_COUNT = 7; // Total number of rooms in enum
    ThermostatData roomThermostatData[ROOM_COUNT];
    
    // Callback functions
    TemperatureChangeCallback onTemperatureChange;
    TemperatureSetCallback onTemperatureSet;
    RoomChangeCallback onRoomChange;
    
    // Static event handlers (required for LVGL callbacks)
    static void arc_event_handler_static(lv_event_t * e);
    static void arc_release_event_handler_static(lv_event_t * e);
    static void btn_event_handler_static(lv_event_t * e);
    
    // Instance pointer for static callbacks
    static TemperatureDisplay* instance;
    
    // Internal event handlers
    void handleArcValueChange(lv_event_t * e);
    void handleArcRelease(lv_event_t * e);
    void handleButtonClick(lv_event_t * e);
    
    // Helper methods
    void updateAllButtonStates();
    void updateTemperatureDisplay();
    void updateRoomDisplay();
    
    // Array index conversion
    int roomToIndex(Room room) const;

public:
    // Constructor
    TemperatureDisplay();
    
    // Destructor
    ~TemperatureDisplay();
    
    // Initialization methods
    bool init();
    bool begin();
    void setupUI();
    
    // Room management
    Room getCurrentRoom() const { return currentRoom; }
    void setCurrentRoom(Room room);
    const char* roomToString(Room room) const;
    
    // Temperature management
    float getCurrentTemperature() const { return currentTemperature; }
    float getTargetTemperature() const { return targetTemperature; }
    void setCurrentTemperature(float temp);
    void setTargetTemperature(float temp);
    
    // Callback setters
    void setTemperatureChangeCallback(TemperatureChangeCallback callback) { 
        onTemperatureChange = callback; 
    }
    void setTemperatureSetCallback(TemperatureSetCallback callback) { 
        onTemperatureSet = callback; 
    }
    void setRoomChangeCallback(RoomChangeCallback callback) { 
        onRoomChange = callback; 
    }
    
    // Update methods
    void updateTime(long currentTime);
    void updateIsConnected(bool isConnected);
    void updateOutsideTemperature(float outsideTemp);
    void updateRoomData(const ThermostatData& thermostatData, Room room);

    // Status panel methods
    void updateStatusPanel(Status status = Status::NONE);
    void updateTransferProgress(uint8_t progress);
    
    // Thermostat data management
    void storeThermostatData(Room room, const ThermostatData& data);
    const ThermostatData& getThermostatData(Room room) const;
    bool hasValidThermostatData(Room room) const;
    void updateSelectedRoomData();

    // Time management
    void configureTimezone(const char* timezone = "CET-1CEST,M3.5.0,M10.5.0/3");
    
    // Utility methods
    void lock() { lvgl_port_lock(-1); }
    void unlock() { lvgl_port_unlock(); }
};

#endif // TEMPERATURE_DISPLAY_H
