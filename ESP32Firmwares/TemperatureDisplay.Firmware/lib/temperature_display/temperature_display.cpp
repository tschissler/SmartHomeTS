#include "temperature_display.h"
#include "thermostat_data.h"

// Static member initialization
TemperatureDisplay *TemperatureDisplay::instance = nullptr;

TemperatureDisplay::TemperatureDisplay()
    : board(nullptr),
      currentRoom(Room::Wohnzimmer),
      currentTemperature(18.6f),
      targetTemperature(22.0f),
      onTemperatureChange(nullptr),
      onTemperatureSet(nullptr),
      onRoomChange(nullptr)
{

    // Set the static instance pointer for callbacks
    instance = this;
}

TemperatureDisplay::~TemperatureDisplay()
{
    if (board)
    {
        delete board;
    }
    instance = nullptr;
}

bool TemperatureDisplay::init()
{
    Serial.println("Initializing Temperature Display");

    // Initialize board
    board = new Board();
    if (!board)
    {
        Serial.println("Failed to create board instance");
        return false;
    }

    board->init();
    return true;
}

bool TemperatureDisplay::begin()
{
    if (!board)
    {
        Serial.println("Board not initialized");
        return false;
    }

    // Start the board
    if (!board->begin())
    {
        Serial.println("Failed to start the board");
        return false;
    }

    // Initialize LVGL
    Serial.println("Initializing LVGL");
    lvgl_port_init(board->getLCD(), board->getTouch());

    // Configure timezone for Berlin (Central European Time)
    configureTimezone();

    board->getLCD()->setDisplayOnOff(true);
    return true;
}

void TemperatureDisplay::setupUI(unsigned long displayTimeoutSec = 60)
{
    Serial.println("Setting up UI");

    displayTimeout = displayTimeoutSec * 1000; 
    lastActivityTime = millis();

    lock();

    // Initialize UI
    ui_init();

    // Add event handlers
    lv_obj_add_event_cb(ui_mainScreen, scr_event_handler_static, LV_EVENT_CLICKED, NULL);
    lv_obj_add_event_cb(ui_arcTargetTemp, arc_event_handler_static, LV_EVENT_VALUE_CHANGED, NULL);
    lv_obj_add_event_cb(ui_arcTargetTemp, arc_release_event_handler_static, LV_EVENT_RELEASED, NULL);
    lv_obj_add_event_cb(ui_btnLivingroom, btn_event_handler_static, LV_EVENT_CLICKED, (void *)Room::Wohnzimmer);
    lv_obj_add_event_cb(ui_btnDiningroom, btn_event_handler_static, LV_EVENT_CLICKED, (void *)Room::Esszimmer);
    lv_obj_add_event_cb(ui_btnKitchen, btn_event_handler_static, LV_EVENT_CLICKED, (void *)Room::Kueche);
    lv_obj_add_event_cb(ui_btnGuestroom, btn_event_handler_static, LV_EVENT_CLICKED, (void *)Room::Gaestezimmer);
    lv_obj_add_event_cb(ui_btnStudy, btn_event_handler_static, LV_EVENT_CLICKED, (void *)Room::Buero);
    lv_obj_add_event_cb(ui_btnTransfer, btn_transfer_event_handler_static, LV_EVENT_CLICKED, NULL);

    // Set initial values
    setCurrentRoom(currentRoom);

    unlock();

    Serial.println("UI setup complete");
}

const char *TemperatureDisplay::roomToString(Room room) const
{
    switch (room)
    {
    case Room::Wohnzimmer:
        return "Wohnzimmer";
    case Room::Esszimmer:
        return "Esszimmer";
    case Room::Kueche:
        return "Kueche";
    case Room::Gaestezimmer:
        return "Gaestzimmer";
    case Room::Buero:
        return "Buero";
    case Room::Schlafzimmer:
        return "Schlafzimmer";
    case Room::Bad:
        return "Bad";
    default:
        return "Unknown";
    }
}

bool TemperatureDisplay::isDisplayTimeoutExceeded() const
{
    return (millis() - lastActivityTime) > displayTimeout;
}

void TemperatureDisplay::updateStatusPanel(Status status)
{
    // Update the status panel based on the current status
    switch (status)
    {
    case Status::NONE:
        lv_obj_set_flag(ui_pnlTransferData, LV_OBJ_FLAG_HIDDEN, true);
        break;
    case Status::TRANSFER:
        lv_label_set_text(ui_lblActivity, "Einstellungen werden an das Gerät übertragen");
        lv_obj_set_flag(ui_pnlTransferData, LV_OBJ_FLAG_HIDDEN, false);
        break;
    case Status::ERROR:
        Serial.println("Status: ERROR");
        break;
    case Status::UPDATE:
        lv_obj_set_flag(ui_pnlTransferData, LV_OBJ_FLAG_HIDDEN, false);
        lv_label_set_text(ui_lblActivity, "Update wird durchgeführt");
        break;
    default:
        Serial.println("Status: UNKNOWN");
        break;
    }
}

void TemperatureDisplay::updateTransferProgress()
{
    if (!transferInProgress) return;
    transferProgress--;
    if (transferProgress == 0) {
      transferProgress = 100; 
    } else {
        lv_bar_set_value(ui_pgbTransferData, transferProgress, LV_ANIM_ON);
    }
}

int TemperatureDisplay::roomToIndex(Room room) const
{
    return static_cast<int>(room);
}

void TemperatureDisplay::setCurrentRoom(Room room)
{
    Room oldRoom = currentRoom;
    currentRoom = room;

    updateAllButtonStates();
    updateSelectedRoomData();

    // Call callback if set
    if (onRoomChange)
    {
        onRoomChange(oldRoom, currentRoom);
    }

    Serial.printf("Room changed to: %s\n", roomToString(currentRoom));
}

void TemperatureDisplay::updateAllButtonStates()
{
    lock();

    // Clear all button states first
    lv_obj_clear_state(ui_btnLivingroom, LV_STATE_CHECKED);
    lv_obj_clear_state(ui_btnDiningroom, LV_STATE_CHECKED);
    lv_obj_clear_state(ui_btnKitchen, LV_STATE_CHECKED);
    lv_obj_clear_state(ui_btnStudy, LV_STATE_CHECKED);
    lv_obj_clear_state(ui_btnGuestroom, LV_STATE_CHECKED);

    // Set the current room button as checked
    lv_obj_t *currentBtn = nullptr;
    switch (currentRoom)
    {
    case Room::Wohnzimmer:
        currentBtn = ui_btnLivingroom;
        break;
    case Room::Esszimmer:
        currentBtn = ui_btnDiningroom;
        break;
    case Room::Kueche:
        currentBtn = ui_btnKitchen;
        break;
    case Room::Gaestezimmer:
        currentBtn = ui_btnGuestroom;
        break;
    case Room::Buero:
        currentBtn = ui_btnStudy;
        break;
    }

    if (currentBtn)
    {
        lv_obj_add_state(currentBtn, LV_STATE_CHECKED);
    }
    unlock();
}

void TemperatureDisplay::updateOutsideTemperature(float outsideTemp)
{
    char temp_str[20];
    snprintf(temp_str, sizeof(temp_str), "%.1f°C", outsideTemp);
    lv_label_set_text(ui_lblTempOutside, temp_str);
    Serial.printf("Outside temperature updated to: %.1f°C\n", outsideTemp);
}

void TemperatureDisplay::updateOutsideGardenTemperature(float outsideTemp)
{
    char temp_str[20];
    snprintf(temp_str, sizeof(temp_str), "%.1f°C", outsideTemp);
    lv_label_set_text(ui_lblTempOutsideGarden, temp_str);
    Serial.printf("Outside garden temperature updated to: %.1f°C\n", outsideTemp);
}

void TemperatureDisplay::updateRoomData(const ThermostatData& thermostatData, Room room)
{
    lv_obj_t *currentTempLabel = nullptr;
    lv_obj_t *targetTempLabel = nullptr;
    lv_obj_t *batteryLabel = nullptr;

    storeThermostatData(room, thermostatData);
    if (room == currentRoom)
    {
        updateSelectedRoomData();
        if (thermostatData.getTargetTemperature() == targetTempSet)
        {
            transferInProgress = false;
            updateStatusPanel(Status::NONE);
        }
    }

    switch (room)
    {
    case Room::Wohnzimmer:
        currentTempLabel = ui_lblCurrentTempLivingroom;
        targetTempLabel = ui_lblTargetTempLivingroom;
        batteryLabel = ui_lblBatteryLivingroom;
        break;
    case Room::Gaestezimmer:
        currentTempLabel = ui_lblCurrentTempGuestroom;
        targetTempLabel = ui_lblTargetTempGuestroom;
        batteryLabel = ui_lblBatteryGuestroom;
        break;
    case Room::Buero:
        currentTempLabel = ui_lblCurrentTempStudy;
        targetTempLabel = ui_lblTargetTempStudy;
        batteryLabel = ui_lblBatteryStudy;
        break;
    case Room::Esszimmer:
        currentTempLabel = ui_lblCurrentTempDiningroom;
        targetTempLabel = ui_lblTargetTempDiningroom;
        batteryLabel = ui_lblBatteryDiningroom;
        break;
    case Room::Kueche:
        currentTempLabel = ui_lblCurrentTempKitchen;
        targetTempLabel = ui_lblTargetTempKitchen;
        batteryLabel = ui_lblBatteryKitchen;
        break;
    default:
        Serial.printf("Unknown room: %d\n", static_cast<int>(room));
    }

    float currentTemp = thermostatData.getCurrentTemperature();
    char temp_str[20];
    snprintf(temp_str, sizeof(temp_str), "%.1f°C", currentTemp);
    lv_label_set_text(currentTempLabel, temp_str);

    float targetTemp = thermostatData.getTargetTemperature();
    char targetTemp_str[20];
    snprintf(targetTemp_str, sizeof(targetTemp_str), "/ %.1f°C", targetTemp);
    lv_label_set_text(targetTempLabel, targetTemp_str);

    float batteryLevel = thermostatData.getBatteryLevel();
    char battery_str[20];
    snprintf(battery_str, sizeof(battery_str), "%d%%", (int)batteryLevel);
    lv_label_set_text(batteryLabel, battery_str);
}

void TemperatureDisplay::updateSelectedRoomData()
{
    // Update the current room's thermostat data display
    const ThermostatData& data = getThermostatData(currentRoom);
    
    char currentTempStr[20];
    snprintf(currentTempStr, sizeof(currentTempStr), "%.1f°C", data.getCurrentTemperature());
    lv_label_set_text(ui_lblCurrentTemp, currentTempStr);

    char targetTempStr[20];
    snprintf(targetTempStr, sizeof(targetTempStr), "%.1f°C", data.getTargetTemperature());
    lv_label_set_text(ui_lblTargetTemp, targetTempStr);
    lv_arc_set_value(ui_arcTargetTemp, static_cast<int>(data.getTargetTemperature() * 2)); // Assuming arc value is in 0.5°C steps

    char valveOpenStr[20];
    snprintf(valveOpenStr, sizeof(valveOpenStr), "%d%%", (int)data.getValvePosition());
    lv_label_set_text(ui_lblValveOpen, valveOpenStr);

}

void TemperatureDisplay::updateIsConnected(bool isConnected)
{
    if (isConnected)
         lv_obj_clear_state(ui_iconWifi, LV_STATE_DISABLED);
    else
         lv_obj_add_state(ui_iconWifi, LV_STATE_DISABLED);
}

void TemperatureDisplay::updateTime(long currentTime)
{
    char timeStr[20];
    time_t t = static_cast<time_t>(currentTime);

    lock();
    struct tm timeinfo;
    localtime_r(&t, &timeinfo); // Thread-safe version of localtime
    strftime(timeStr, sizeof(timeStr), "%H:%M", &timeinfo);
    lv_label_set_text(ui_lblTime, timeStr);
    unlock();
}

void TemperatureDisplay::updateVersion(String version)
{
    lock();
    lv_label_set_text(ui_lblVersion, version.c_str());
    unlock();
}

void TemperatureDisplay::configureTimezone(const char *timezone)
{
    Serial.printf("Setting timezone to: %s\n", timezone);
    setenv("TZ", timezone, 1);
    tzset();
    Serial.println("Timezone configured");
}

// Static event handlers for LVGL callbacks
void TemperatureDisplay::arc_event_handler_static(lv_event_t *e)
{
    if (instance) {
        instance->lastActivityTime = millis();
        if(instance->isDisplayOn) {
           instance->handleArcValueChange(e);
        }
    }
}

void TemperatureDisplay::arc_release_event_handler_static(lv_event_t *e)
{
    if (instance) {
        instance->lastActivityTime = millis();
        if(instance->isDisplayOn) {
           instance->handleArcRelease(e);
        }
    }
}

void TemperatureDisplay::btn_event_handler_static(lv_event_t *e)
{
    if (instance) {
        instance->lastActivityTime = millis();
        if(instance->isDisplayOn) {
           instance->handleRoomButtonClick(e);
        } 
    }
}

void TemperatureDisplay::btn_transfer_event_handler_static(lv_event_t *e)
{
    if (instance) {
        instance->lastActivityTime = millis();
        if(instance->isDisplayOn) {
           instance->handleTransferButtonClick(e);
        } 
    }
}

void TemperatureDisplay::scr_event_handler_static(lv_event_t *e)
{
    if (instance) {
        instance->lastActivityTime = millis();
        if(instance->isDisplayOn) {
           instance->handleScreenEvent(e);
        }
    }
}

// Instance event handlers
void TemperatureDisplay::handleArcValueChange(lv_event_t *e)
{
    lv_obj_t *arc = (lv_obj_t *)lv_event_get_target(e);
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
    if (onTemperatureChange)
    {
        onTemperatureChange(temp, currentRoom);
    }

    Serial.printf("Arc value changed to: %.1f°C for room: %s\n", temp, roomToString(currentRoom));
}

void TemperatureDisplay::handleArcRelease(lv_event_t *e)
{
    lv_obj_t *arc = (lv_obj_t *)lv_event_get_target(e);
    int32_t value = lv_arc_get_value(arc);

    // Convert arc value to temperature
    updatedTargetTemperature = value / 2.0f;
    lv_obj_set_flag(ui_btnTransfer, LV_OBJ_FLAG_HIDDEN, false);

    Serial.printf("Arc released at: %.1f°C for room: %s\n", updatedTargetTemperature, roomToString(currentRoom));
}

void TemperatureDisplay::handleScreenEvent(lv_event_t *e)
{
    board->getBacklight()->on(); 
    isDisplayOn = true;
}

void TemperatureDisplay::handleTransferButtonClick(lv_event_t *e)
{
    updateStatusPanel(Status::TRANSFER);
    transferInProgress = true;
    transferProgress = 100;
    targetTempSet = updatedTargetTemperature;
    updateTransferProgress();

    if (onTemperatureSet)
    {
        onTemperatureSet(updatedTargetTemperature, currentRoom);
    }
    lv_obj_set_flag(ui_btnTransfer, LV_OBJ_FLAG_HIDDEN, true);
}

void TemperatureDisplay::handleRoomButtonClick(lv_event_t *e)
{
    lv_obj_t *btn = (lv_obj_t *)lv_event_get_target(e);
    Room room = static_cast<Room>(reinterpret_cast<intptr_t>(lv_event_get_user_data(e)));

    setCurrentRoom(room);
}

void TemperatureDisplay::turnDisplayOn()
{
    isDisplayOn = true;
    Serial.println("Display turned on");
    board->getBacklight()->on();
}

void TemperatureDisplay::turnDisplayOff()
{
    isDisplayOn = false;
    Serial.println("Display turned off");
    board->getBacklight()->off();
}

// Thermostat data management methods
void TemperatureDisplay::storeThermostatData(Room room, const ThermostatData& data)
{
    int index = roomToIndex(room);
    if (index >= 0 && index < ROOM_COUNT) {
        roomThermostatData[index] = data;
        Serial.printf("Stored thermostat data for %s\n", roomToString(room));
    } else {
        Serial.printf("Invalid room index: %d for room %s\n", index, roomToString(room));
    }
}

const ThermostatData& TemperatureDisplay::getThermostatData(Room room) const
{
    int index = roomToIndex(room);
    if (index >= 0 && index < ROOM_COUNT) {
        return roomThermostatData[index];
    } else {
        Serial.printf("Invalid room index: %d for room %s, returning default data\n", index, roomToString(room));
        static ThermostatData defaultData; // Return a default invalid object
        return defaultData;
    }
}

bool TemperatureDisplay::hasValidThermostatData(Room room) const
{
    const ThermostatData& data = getThermostatData(room);
    return data.getIsValid();
}

