#include <Arduino.h>
#include <esp_display_panel.hpp>
#include <lvgl.h>
#include "lvgl_v9_port.h"
#include "ui.h"

using namespace esp_panel::drivers;
using namespace esp_panel::board;

// Enum for room selection
enum class Room : int {
    Wohnzimmer = 0,
    Esszimmer = 1,
    Gaestezimmer = 2,
    Buero = 3,
    Schlafzimmer = 4,
    Bad = 5
};

// Global variable to track current room
Room currentRoom = Room::Wohnzimmer;

// Function to convert room enum to string
const char* roomToString(Room room) {
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

// Event handler for arc value changes
void arc_event_handler(lv_event_t * e)
{
  lv_obj_t * arc = (lv_obj_t*)lv_event_get_target(e);
  int32_t value = lv_arc_get_value(arc);
  
  // Convert arc value to temperature string
  char temp_str[20];
  snprintf(temp_str, sizeof(temp_str), "%.1f°C", value / 2.0);
  // Update the target temperature label
  lv_label_set_text(ui_lblTargetTemp, temp_str);
  
  Serial.printf("Arc value changed to: %.1fd\n", value / 2.0);
}

void btn_event_handler(lv_event_t * e)
{
  lv_obj_t * btn = (lv_obj_t*)lv_event_get_target(e);
  Room room = static_cast<Room>(reinterpret_cast<intptr_t>(lv_event_get_user_data(e)));
  
  // Uncheck all buttons
  lv_obj_clear_state(ui_btnWohnzimmer, LV_STATE_CHECKED);
  lv_obj_clear_state(ui_btnEsszimmer, LV_STATE_CHECKED);
  lv_obj_clear_state(ui_btnGaestezimmer, LV_STATE_CHECKED);
  lv_obj_clear_state(ui_btnBuero, LV_STATE_CHECKED);
  lv_obj_clear_state(ui_btnSchlafzimmer, LV_STATE_CHECKED);
  lv_obj_clear_state(ui_btnBad, LV_STATE_CHECKED);

  // Change current room
  currentRoom = room;
  
  // Log the room change
  Serial.printf("Button pressed for room: %s\n", roomToString(currentRoom));
  
  lv_obj_set_state(btn, LV_STATE_CHECKED, true);
}

void setup()
{
  Serial.begin(115200);
  Serial.println("------------------  Temperature Display Firmware  ------------------");
  
  // Initialize board
  Board *board = new Board();
  board->init();
  
  // Start the board
  assert(board->begin() && "Failed to start the board");

  // Initialize LVGL
  Serial.println("Initializing LVGL");
  lvgl_port_init(board->getLCD(), board->getTouch());

  // Initialize UI
  Serial.println("Creating UI");
  lvgl_port_lock(-1);
  ui_init();
  
  // Add event handlers 
  lv_obj_add_event_cb(ui_arcTargetTemp, arc_event_handler, LV_EVENT_VALUE_CHANGED, NULL);
  lv_obj_add_event_cb(ui_btnWohnzimmer, btn_event_handler, LV_EVENT_CLICKED, (void*)Room::Wohnzimmer);
  lv_obj_add_event_cb(ui_btnEsszimmer, btn_event_handler, LV_EVENT_CLICKED, (void*)Room::Esszimmer);
  lv_obj_add_event_cb(ui_btnGaestezimmer, btn_event_handler, LV_EVENT_CLICKED, (void*)Room::Gaestezimmer);
  lv_obj_add_event_cb(ui_btnBuero, btn_event_handler, LV_EVENT_CLICKED, (void*)Room::Buero);
  lv_obj_add_event_cb(ui_btnSchlafzimmer, btn_event_handler, LV_EVENT_CLICKED, (void*)Room::Schlafzimmer);
  lv_obj_add_event_cb(ui_btnBad, btn_event_handler, LV_EVENT_CLICKED, (void*)Room::Bad);
  
  // Set initial values
  lv_arc_set_value(ui_arcTargetTemp, 22); // Set initial target temperature
  lv_label_set_text(ui_lblTargetTemp, "22,6°C"); // Set initial label text
  lv_label_set_text(ui_lblcurrentTemp, "18,6°C"); // Set current temperature
  
  lvgl_port_unlock();

  Serial.printf("Initialization complete");
}

void loop()
{
  // Simulate reading temperature sensor
  static float current_temp = 18.6;
  static unsigned long last_update = 0;
  
  // Update current temperature every 5 seconds
  if (millis() - last_update > 5000) {
    current_temp += (random(-10, 11) / 10.0); // Simulate temperature fluctuation
    if (current_temp < 5) current_temp = 5;
    if (current_temp > 35) current_temp = 35;

    char temp_str[20];
    snprintf(temp_str, sizeof(temp_str), "%.1f°C", current_temp);
    
    lvgl_port_lock(-1);
    lv_label_set_text(ui_lblcurrentTemp, temp_str);
    lvgl_port_unlock();
    
    last_update = millis();
    Serial.printf("Current temperature updated to: %.1f°C\n", current_temp);
  }
  
  delay(100); // Small delay to prevent excessive CPU usage
}
