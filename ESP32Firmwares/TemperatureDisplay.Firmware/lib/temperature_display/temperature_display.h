#ifndef TEMPERATURE_DISPLAY_H
#define TEMPERATURE_DISPLAY_H

#include <Arduino.h>
#include <esp_display_panel.hpp>
#include <esp_io_expander.hpp>
#include <lvgl.h>
#include <time.h>
#include <sys/time.h>
#include "lvgl_v9_port.h"
#include "ui.h"
#include "thermostat_data.h"

// Extension IO pin definition
#define TP_RST 1      // Touch screen reset pin
#define LCD_BL 2      // LCD backlight pinout
#define LCD_RST 3     // LCD reset pin
#define SD_CS 4       // SD card select pin
#define USB_SEL 5     // USB select pin

// I2C Pin define 
#define I2C_MASTER_NUM I2C_NUM_0 // I2C master number
#define I2C_MASTER_SDA_IO 8       // I2C data line
#define I2C_MASTER_SCL_IO 9     

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
    UPDATE,  
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
    float updatedTargetTemperature;
    unsigned long lastActivityTime = 0;
    unsigned long displayTimeout = 0;

    static const int ROOM_COUNT = 7;
    ThermostatData roomThermostatData[ROOM_COUNT];
    
    // Callback functions
    TemperatureChangeCallback onTemperatureChange;
    TemperatureSetCallback onTemperatureSet;
    RoomChangeCallback onRoomChange;
    
    // Static event handlers (required for LVGL callbacks)
    static void scr_event_handler_static(lv_event_t * e);
    static void arc_event_handler_static(lv_event_t * e);
    static void arc_release_event_handler_static(lv_event_t * e);
    static void btn_event_handler_static(lv_event_t * e);
    static void btn_transfer_event_handler_static(lv_event_t *e);

    // Instance pointer for static callbacks
    static TemperatureDisplay* instance;
    
    // Internal event handlers
    void handleScreenEvent(lv_event_t * e);
    void handleTransferButtonClick(lv_event_t *e);
    void handleArcValueChange(lv_event_t *e);
    void handleArcRelease(lv_event_t * e);
    void handleRoomButtonClick(lv_event_t * e);
    
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
    void setupUI(unsigned long displayTimeoutMs);
    
    // Room management
    Room getCurrentRoom() const { return currentRoom; }
    void setCurrentRoom(Room room);
    const char* roomToString(Room room) const;
    
    // Temperature management
    float getCurrentTemperature() const { return currentTemperature; }
    float getTargetTemperature() const { return targetTemperature; }
    void setCurrentTemperature(float temp);
    void setTargetTemperature(float temp);

    // Transfer progress management
    bool transferInProgress = false;
    float targetTempSet = 0.0f;
    uint8_t transferProgress = 0;

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
    void updateVersion(String version);
    void updateIsConnected(bool isConnected);
    void updateOutsideTemperature(float outsideTemp);
    void updateOutsideGardenTemperature(float outsideTemp);
    void updateRoomData(const ThermostatData &thermostatData, Room room);

    // Status panel methods
    void updateStatusPanel(Status status = Status::NONE);
    void updateTransferProgress();
    
    // Thermostat data management
    void storeThermostatData(Room room, const ThermostatData& data);
    const ThermostatData& getThermostatData(Room room) const;
    bool hasValidThermostatData(Room room) const;
    void updateSelectedRoomData();

    // Time management
    void configureTimezone(const char* timezone = "CET-1CEST,M3.5.0,M10.5.0/3");
    
    // Utility methods
    bool isDisplayOn = true;
    void lock() { lvgl_port_lock(-1); }
    void unlock() { lvgl_port_unlock(); }
    void turnDisplayOn();
    void turnDisplayOff();
    bool isDisplayTimeoutExceeded() const;
};

#endif // TEMPERATURE_DISPLAY_H
