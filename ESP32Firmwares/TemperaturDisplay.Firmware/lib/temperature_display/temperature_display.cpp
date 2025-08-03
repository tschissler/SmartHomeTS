#include "temperature_display.h"

// Static member initialization
TemperatureDisplay* TemperatureDisplay::instance = nullptr;

TemperatureDisplay::TemperatureDisplay() 
    : board(nullptr), 
      currentRoom(Room::Wohnzimmer),
      currentTemperature(18.6f),
      targetTemperature(22.0f),
      onTemperatureChange(nullptr),
      onRoomChange(nullptr) {
    
    // Set the static instance pointer for callbacks
    instance = this;
}

TemperatureDisplay::~TemperatureDisplay() {
    if (board) {
        delete board;
    }
    instance = nullptr;
}

bool TemperatureDisplay::init() {
    Serial.println("Initializing Temperature Display");
    
    // Initialize board
    board = new Board();
    if (!board) {
        Serial.println("Failed to create board instance");
        return false;
    }
    
    board->init();
    return true;
}

bool TemperatureDisplay::begin() {
    if (!board) {
        Serial.println("Board not initialized");
        return false;
    }
    
    // Start the board
    if (!board->begin()) {
        Serial.println("Failed to start the board");
        return false;
    }

    // Initialize LVGL
    Serial.println("Initializing LVGL");
    lvgl_port_init(board->getLCD(), board->getTouch());
    
    return true;
}

void TemperatureDisplay::setupUI() {
    Serial.println("Setting up UI");
    
    lock();
    
    // Initialize UI
    ui_init();
    
    // Add event handlers 
    lv_obj_add_event_cb(ui_arcTargetTemp, arc_event_handler_static, LV_EVENT_VALUE_CHANGED, NULL);
    lv_obj_add_event_cb(ui_btnWohnzimmer, btn_event_handler_static, LV_EVENT_CLICKED, (void*)Room::Wohnzimmer);
    lv_obj_add_event_cb(ui_btnEsszimmer, btn_event_handler_static, LV_EVENT_CLICKED, (void*)Room::Esszimmer);
    lv_obj_add_event_cb(ui_btnGaestezimmer, btn_event_handler_static, LV_EVENT_CLICKED, (void*)Room::Gaestezimmer);
    lv_obj_add_event_cb(ui_btnBuero, btn_event_handler_static, LV_EVENT_CLICKED, (void*)Room::Buero);
    lv_obj_add_event_cb(ui_btnSchlafzimmer, btn_event_handler_static, LV_EVENT_CLICKED, (void*)Room::Schlafzimmer);
    lv_obj_add_event_cb(ui_btnBad, btn_event_handler_static, LV_EVENT_CLICKED, (void*)Room::Bad);
    
    // Set initial values
    setTargetTemperature(targetTemperature);
    setCurrentTemperature(currentTemperature);
    setCurrentRoom(currentRoom);
    
    unlock();
    
    Serial.println("UI setup complete");
}

const char* TemperatureDisplay::roomToString(Room room) const {
    switch(room) {
        case Room::Wohnzimmer: return "Wohnzimmer";
        case Room::Esszimmer: return "Esszimmer";
        case Room::Gaestezimmer: return "Gästezimmer";
        case Room::Buero: return "Büro";
        case Room::Schlafzimmer: return "Schlafzimmer";
        case Room::Bad: return "Bad";
        default: return "Unknown";
    }
}

void TemperatureDisplay::setCurrentRoom(Room room) {
    Room oldRoom = currentRoom;
    currentRoom = room;
    
    updateAllButtonStates();
    updateRoomDisplay();
    
    // Call callback if set
    if (onRoomChange) {
        onRoomChange(oldRoom, currentRoom);
    }
    
    Serial.printf("Room changed to: %s\n", roomToString(currentRoom));
}

void TemperatureDisplay::setCurrentTemperature(float temp) {
    currentTemperature = temp;
    updateTemperatureDisplay();
    
    Serial.printf("Current temperature updated to: %.1f°C\n", currentTemperature);
}

void TemperatureDisplay::setTargetTemperature(float temp) {
    targetTemperature = temp;
    
    lock();
    lv_arc_set_value(ui_arcTargetTemp, (int)(temp * 2)); // Convert to arc value
    
    char temp_str[20];
    snprintf(temp_str, sizeof(temp_str), "%.1f°C", temp);
    lv_label_set_text(ui_lblTargetTemp, temp_str);
    unlock();
    
    // Call callback if set
    if (onTemperatureChange) {
        onTemperatureChange(temp, currentRoom);
    }
    
    Serial.printf("Target temperature set to: %.1f°C for room: %s\n", temp, roomToString(currentRoom));
}

void TemperatureDisplay::updateTemperatureDisplay() {
    char temp_str[20];
    snprintf(temp_str, sizeof(temp_str), "%.1f°C", currentTemperature);
    
    lock();
    lv_label_set_text(ui_lblcurrentTemp, temp_str);
    unlock();
}

void TemperatureDisplay::updateAllButtonStates() {
    lock();
    
    // Clear all button states first
    lv_obj_clear_state(ui_btnWohnzimmer, LV_STATE_CHECKED);
    lv_obj_clear_state(ui_btnEsszimmer, LV_STATE_CHECKED);
    lv_obj_clear_state(ui_btnGaestezimmer, LV_STATE_CHECKED);
    lv_obj_clear_state(ui_btnBuero, LV_STATE_CHECKED);
    lv_obj_clear_state(ui_btnSchlafzimmer, LV_STATE_CHECKED);
    lv_obj_clear_state(ui_btnBad, LV_STATE_CHECKED);
    
    // Set the current room button as checked
    lv_obj_t* currentBtn = nullptr;
    switch(currentRoom) {
        case Room::Wohnzimmer: currentBtn = ui_btnWohnzimmer; break;
        case Room::Esszimmer: currentBtn = ui_btnEsszimmer; break;
        case Room::Gaestezimmer: currentBtn = ui_btnGaestezimmer; break;
        case Room::Buero: currentBtn = ui_btnBuero; break;
        case Room::Schlafzimmer: currentBtn = ui_btnSchlafzimmer; break;
        case Room::Bad: currentBtn = ui_btnBad; break;
    }
    
    if (currentBtn) {
        lv_obj_add_state(currentBtn, LV_STATE_CHECKED);
    }
    
    unlock();
}

void TemperatureDisplay::updateRoomDisplay() {
    // This method can be extended if you have a room label on the display
    // For now, it just updates the button states
    updateAllButtonStates();
}

void TemperatureDisplay::update() {
    // This method can be called regularly to update the display
    // Currently, LVGL handles most updates automatically
    delay(10); // Small delay to prevent excessive CPU usage
}

void TemperatureDisplay::simulateTemperatureSensor() {
    static unsigned long last_update = 0;
    
    // Update current temperature every 5 seconds
    if (millis() - last_update > 5000) {
        float temp_change = (random(-10, 11) / 10.0f); // Simulate temperature fluctuation
        float new_temp = currentTemperature + temp_change;
        
        // Clamp temperature to reasonable range
        if (new_temp < 5) new_temp = 5;
        if (new_temp > 35) new_temp = 35;
        
        setCurrentTemperature(new_temp);
        last_update = millis();
    }
}

// Static event handlers for LVGL callbacks
void TemperatureDisplay::arc_event_handler_static(lv_event_t * e) {
    if (instance) {
        instance->handleArcValueChange(e);
    }
}

void TemperatureDisplay::btn_event_handler_static(lv_event_t * e) {
    if (instance) {
        instance->handleButtonClick(e);
    }
}

// Instance event handlers
void TemperatureDisplay::handleArcValueChange(lv_event_t * e) {
    lv_obj_t * arc = (lv_obj_t*)lv_event_get_target(e);
    int32_t value = lv_arc_get_value(arc);
    
    // Convert arc value to temperature
    float temp = value / 2.0f;
    targetTemperature = temp;
    
    // Update the target temperature label
    char temp_str[20];
    snprintf(temp_str, sizeof(temp_str), "%.1f°C", temp);
    
    lock();
    lv_label_set_text(ui_lblTargetTemp, temp_str);
    unlock();
    
    // Call callback if set
    if (onTemperatureChange) {
        onTemperatureChange(temp, currentRoom);
    }
    
    Serial.printf("Arc value changed to: %.1f°C for room: %s\n", temp, roomToString(currentRoom));
}

void TemperatureDisplay::handleButtonClick(lv_event_t * e) {
    lv_obj_t * btn = (lv_obj_t*)lv_event_get_target(e);
    Room room = static_cast<Room>(reinterpret_cast<intptr_t>(lv_event_get_user_data(e)));
    
    setCurrentRoom(room);
}
