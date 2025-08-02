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
  Board *board = new Board();
  board->init();
#if LVGL_PORT_AVOID_TEARING_MODE
  auto lcd = board->getLCD();
  // When avoid tearing function is enabled, the frame buffer number should be set in the board driver
  lcd->configFrameBufferNumber(LVGL_PORT_DISP_BUFFER_NUM);
#if ESP_PANEL_DRIVERS_BUS_ENABLE_RGB && CONFIG_IDF_TARGET_ESP32S3
  auto lcd_bus = lcd->getBus();
  /**
   * As the anti-tearing feature typically consumes more PSRAM bandwidth, for the ESP32-S3, we need to utilize the
   * "bounce buffer" functionality to enhance the RGB data bandwidth.
   * This feature will consume `bounce_buffer_size * bytes_per_pixel * 2` of SRAM memory.
   */
  if (lcd_bus->getBasicAttributes().type == ESP_PANEL_BUS_TYPE_RGB)
  {
    static_cast<BusRGB *>(lcd_bus)->configRGB_BounceBufferSize(lcd->getFrameWidth() * 10);
  }
#endif
#endif

  assert(board->begin() && "Failed to start the board");

  Serial.println("Initializing LVGL");
  lvgl_port_init(board->getLCD(), board->getTouch());

  Serial.println("Creating UI");
  /* Lock the mutex due to the LVGL APIs are not thread-safe */
  lvgl_port_lock(-1);

  /**
   * Create the simple labels
   */
  // lv_obj_t *label_1 = lv_label_create(lv_scr_act());
  // lv_label_set_text(label_1, "Hello World!");
  // lv_obj_set_style_text_font(label_1, &lv_font_montserrat_14, 0);
  // lv_obj_align(label_1, LV_ALIGN_CENTER, 0, -20);
  // lv_obj_t *label_2 = lv_label_create(lv_scr_act());
  // lv_label_set_text_fmt(
  //     label_2, "ESP32_Display_Panel(%d.%d.%d)",
  //     ESP_PANEL_VERSION_MAJOR, ESP_PANEL_VERSION_MINOR, ESP_PANEL_VERSION_PATCH);
  // lv_obj_set_style_text_font(label_2, &lv_font_montserrat_14, 0);
  // lv_obj_align_to(label_2, label_1, LV_ALIGN_OUT_BOTTOM_MID, 0, 10);
  // lv_obj_t *label_3 = lv_label_create(lv_scr_act());
  // lv_label_set_text_fmt(label_3, "LVGL(%d.%d.%d)", LVGL_VERSION_MAJOR, LVGL_VERSION_MINOR, LVGL_VERSION_PATCH);
  // lv_obj_set_style_text_font(label_3, &lv_font_montserrat_14, 0);
  // lv_obj_align_to(label_3, label_2, LV_ALIGN_OUT_BOTTOM_MID, 0, 10);
    
  ui_init();

  lvgl_port_unlock();
  Serial.println("Initializing end");
}

void loop()
{
  delay(1000); // Wait for 1 second
}
