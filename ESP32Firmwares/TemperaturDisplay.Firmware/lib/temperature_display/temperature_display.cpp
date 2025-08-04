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

    return true;
}

void TemperatureDisplay::setupUI()
{
    Serial.println("Setting up UI");

    lock();

    // Initialize UI
    ui_init();

    // Add event handlers
    lv_obj_add_event_cb(ui_arcTargetTemp, arc_event_handler_static, LV_EVENT_VALUE_CHANGED, NULL);
    lv_obj_add_event_cb(ui_btnLivingroom, btn_event_handler_static, LV_EVENT_CLICKED, (void *)Room::Wohnzimmer);
    lv_obj_add_event_cb(ui_btnDiningroom, btn_event_handler_static, LV_EVENT_CLICKED, (void *)Room::Esszimmer);
    lv_obj_add_event_cb(ui_btnKitchen, btn_event_handler_static, LV_EVENT_CLICKED, (void *)Room::Kueche);
    lv_obj_add_event_cb(ui_btnGuestroom, btn_event_handler_static, LV_EVENT_CLICKED, (void *)Room::Gaestezimmer);
    lv_obj_add_event_cb(ui_btnStudy, btn_event_handler_static, LV_EVENT_CLICKED, (void *)Room::Buero);

    // Set initial values
    setTargetTemperature(targetTemperature);
    setCurrentTemperature(currentTemperature);
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
    case Room::Gaestezimmer:
        return "Gästezimmer";
    case Room::Buero:
        return "Büro";
    case Room::Schlafzimmer:
        return "Schlafzimmer";
    case Room::Bad:
        return "Bad";
    default:
        return "Unknown";
    }
}

void TemperatureDisplay::setCurrentRoom(Room room)
{
    Room oldRoom = currentRoom;
    currentRoom = room;

    updateAllButtonStates();
    updateRoomDisplay();

    // Call callback if set
    if (onRoomChange)
    {
        onRoomChange(oldRoom, currentRoom);
    }

    Serial.printf("Room changed to: %s\n", roomToString(currentRoom));
}

void TemperatureDisplay::setCurrentTemperature(float temp)
{
    currentTemperature = temp;
    updateTemperatureDisplay();

    Serial.printf("Current temperature updated to: %.1f°C\n", currentTemperature);
}

void TemperatureDisplay::setTargetTemperature(float temp)
{
    targetTemperature = temp;

    lock();
    lv_arc_set_value(ui_arcTargetTemp, (int)(temp * 2)); // Convert to arc value

    char temp_str[20];
    snprintf(temp_str, sizeof(temp_str), "%.1f°C", temp);
    lv_label_set_text(ui_lblTargetTemp, temp_str);
    unlock();

    // Call callback if set
    if (onTemperatureChange)
    {
        onTemperatureChange(temp, currentRoom);
    }

    Serial.printf("Target temperature set to: %.1f°C for room: %s\n", temp, roomToString(currentRoom));
}

void TemperatureDisplay::updateTemperatureDisplay()
{
    char temp_str[20];
    snprintf(temp_str, sizeof(temp_str), "%.1f°C", currentTemperature);

    lock();
    lv_label_set_text(ui_lblCurrentTemp, temp_str);
    unlock();
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

void TemperatureDisplay::updateRoomDisplay()
{
    // This method can be extended if you have a room label on the display
    // For now, it just updates the button states
    updateAllButtonStates();
}

void TemperatureDisplay::updateOutsideTemperature(float outsideTemp)
{
    char temp_str[20];
    snprintf(temp_str, sizeof(temp_str), "%.1f°C", outsideTemp);
    lv_label_set_text(ui_lblTempOutside, temp_str);
    Serial.printf("Outside temperature updated to: %.1f°C\n", outsideTemp);
}

void TemperatureDisplay::updateRoomData(const ThermostatData& thermostatData, Room room)
{
    lv_obj_t *currentTempLabel = nullptr;
    lv_obj_t *targetTempLabel = nullptr;
    lv_obj_t *batteryLabel = nullptr;
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
    snprintf(targetTemp_str, sizeof(targetTemp_str), "%.1f°C", targetTemp);
    lv_label_set_text(targetTempLabel, targetTemp_str);

    float batteryLevel = thermostatData.getBatteryLevel();
    char battery_str[20];
    snprintf(battery_str, sizeof(battery_str), "%d%%", (int)batteryLevel);
    lv_label_set_text(batteryLabel, battery_str);
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

void TemperatureDisplay::configureTimezone(const char *timezone)
{
    Serial.printf("Setting timezone to: %s\n", timezone);
    setenv("TZ", timezone, 1);
    tzset();
    Serial.println("Timezone configured");
}

void TemperatureDisplay::update()
{
    // This method can be called regularly to update the display
    // Currently, LVGL handles most updates automatically
    delay(10); // Small delay to prevent excessive CPU usage
}

// Static event handlers for LVGL callbacks
void TemperatureDisplay::arc_event_handler_static(lv_event_t *e)
{
    if (instance)
    {
        instance->handleArcValueChange(e);
    }
}

void TemperatureDisplay::btn_event_handler_static(lv_event_t *e)
{
    if (instance)
    {
        instance->handleButtonClick(e);
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

void TemperatureDisplay::handleButtonClick(lv_event_t *e)
{
    lv_obj_t *btn = (lv_obj_t *)lv_event_get_target(e);
    Room room = static_cast<Room>(reinterpret_cast<intptr_t>(lv_event_get_user_data(e)));

    setCurrentRoom(room);
}
