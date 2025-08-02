#include <Arduino.h>
#include <esp_display_panel.hpp>
#include <lvgl.h>
#include "lvgl_v9_port.h"
#include "ui.h"

using namespace esp_panel::drivers;
using namespace esp_panel::board;

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
  lvgl_port_unlock();
  
  Serial.println("Initialization complete");
}

void loop()
{
  delay(1000); // Wait for 1 second
}
