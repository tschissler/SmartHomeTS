// Function to initialize the LCD
#include "waveshare_display.h"
#include <lvgl.h>
#include "lvgl_v9_port.h"
#include "ui.h"
#include <esp_display_panel.hpp>

using namespace esp_panel::drivers;
using namespace esp_panel::board;

static LCD *create_lcd_without_config(void)
{
    BusRGB *bus = new BusRGB(
#if EXAMPLE_LCD_RGB_DATA_WIDTH == 8
        /* 8-bit RGB IOs */
        EXAMPLE_LCD_RGB_IO_DATA0, EXAMPLE_LCD_RGB_IO_DATA1, EXAMPLE_LCD_RGB_IO_DATA2, EXAMPLE_LCD_RGB_IO_DATA3,
        EXAMPLE_LCD_RGB_IO_DATA4, EXAMPLE_LCD_RGB_IO_DATA5, EXAMPLE_LCD_RGB_IO_DATA6, EXAMPLE_LCD_RGB_IO_DATA7,
        EXAMPLE_LCD_RGB_IO_HSYNC, EXAMPLE_LCD_RGB_IO_VSYNC, EXAMPLE_LCD_RGB_IO_PCLK, EXAMPLE_LCD_RGB_IO_DE,
        EXAMPLE_LCD_RGB_IO_DISP,
        /* RGB timings */
        EXAMPLE_LCD_RGB_TIMING_FREQ_HZ, EXAMPLE_LCD_WIDTH, EXAMPLE_LCD_HEIGHT,
        EXAMPLE_LCD_RGB_TIMING_HPW, EXAMPLE_LCD_RGB_TIMING_HBP, EXAMPLE_LCD_RGB_TIMING_HFP,
        EXAMPLE_LCD_RGB_TIMING_VPW, EXAMPLE_LCD_RGB_TIMING_VBP, EXAMPLE_LCD_RGB_TIMING_VFP
#elif EXAMPLE_LCD_RGB_DATA_WIDTH == 16
        /* 16-bit RGB IOs */
        EXAMPLE_LCD_RGB_IO_DATA0, EXAMPLE_LCD_RGB_IO_DATA1, EXAMPLE_LCD_RGB_IO_DATA2, EXAMPLE_LCD_RGB_IO_DATA3,
        EXAMPLE_LCD_RGB_IO_DATA4, EXAMPLE_LCD_RGB_IO_DATA5, EXAMPLE_LCD_RGB_IO_DATA6, EXAMPLE_LCD_RGB_IO_DATA7,
        EXAMPLE_LCD_RGB_IO_DATA8, EXAMPLE_LCD_RGB_IO_DATA9, EXAMPLE_LCD_RGB_IO_DATA10, EXAMPLE_LCD_RGB_IO_DATA11,
        EXAMPLE_LCD_RGB_IO_DATA12, EXAMPLE_LCD_RGB_IO_DATA13, EXAMPLE_LCD_RGB_IO_DATA14, EXAMPLE_LCD_RGB_IO_DATA15,
        EXAMPLE_LCD_RGB_IO_HSYNC, EXAMPLE_LCD_RGB_IO_VSYNC, EXAMPLE_LCD_RGB_IO_PCLK, EXAMPLE_LCD_RGB_IO_DE,
        EXAMPLE_LCD_RGB_IO_DISP,
        /* RGB timings */
        EXAMPLE_LCD_RGB_TIMING_FREQ_HZ, EXAMPLE_LCD_WIDTH, EXAMPLE_LCD_HEIGHT,
        EXAMPLE_LCD_RGB_TIMING_HPW, EXAMPLE_LCD_RGB_TIMING_HBP, EXAMPLE_LCD_RGB_TIMING_HFP,
        EXAMPLE_LCD_RGB_TIMING_VPW, EXAMPLE_LCD_RGB_TIMING_VBP, EXAMPLE_LCD_RGB_TIMING_VFP
#endif
    );

    /**
     * Take `ST7262` as an example, the following is the actual code after macro expansion:
     *      LCD_ST7262(bus, 24, -1);
     */
    return new EXAMPLE_LCD_CLASS(
        EXAMPLE_LCD_NAME, bus, EXAMPLE_LCD_WIDTH, EXAMPLE_LCD_HEIGHT, EXAMPLE_LCD_COLOR_BITS, EXAMPLE_LCD_RST_IO
    );
}

static LCD *create_lcd_with_config(void)
{
    BusRGB::Config bus_config = {
        .refresh_panel = BusRGB::RefreshPanelPartialConfig{
            .pclk_hz = EXAMPLE_LCD_RGB_TIMING_FREQ_HZ,
            .h_res = EXAMPLE_LCD_WIDTH,
            .v_res = EXAMPLE_LCD_HEIGHT,
            .hsync_pulse_width = EXAMPLE_LCD_RGB_TIMING_HPW,
            .hsync_back_porch = EXAMPLE_LCD_RGB_TIMING_HBP,
            .hsync_front_porch = EXAMPLE_LCD_RGB_TIMING_HFP,
            .vsync_pulse_width = EXAMPLE_LCD_RGB_TIMING_VPW,
            .vsync_back_porch = EXAMPLE_LCD_RGB_TIMING_VBP,
            .vsync_front_porch = EXAMPLE_LCD_RGB_TIMING_VFP,
            .data_width = EXAMPLE_LCD_RGB_DATA_WIDTH,
            .bits_per_pixel = EXAMPLE_LCD_RGB_COLOR_BITS,
            .bounce_buffer_size_px = EXAMPLE_LCD_RGB_BOUNCE_BUFFER_SIZE,
            .hsync_gpio_num = EXAMPLE_LCD_RGB_IO_HSYNC,
            .vsync_gpio_num = EXAMPLE_LCD_RGB_IO_VSYNC,
            .de_gpio_num = EXAMPLE_LCD_RGB_IO_DE,
            .pclk_gpio_num = EXAMPLE_LCD_RGB_IO_PCLK,
            .disp_gpio_num = EXAMPLE_LCD_RGB_IO_DISP,
            .data_gpio_nums = {
                EXAMPLE_LCD_RGB_IO_DATA0, EXAMPLE_LCD_RGB_IO_DATA1, EXAMPLE_LCD_RGB_IO_DATA2, EXAMPLE_LCD_RGB_IO_DATA3,
                EXAMPLE_LCD_RGB_IO_DATA4, EXAMPLE_LCD_RGB_IO_DATA5, EXAMPLE_LCD_RGB_IO_DATA6, EXAMPLE_LCD_RGB_IO_DATA7,
#if EXAMPLE_LCD_RGB_DATA_WIDTH > 8
                EXAMPLE_LCD_RGB_IO_DATA8, EXAMPLE_LCD_RGB_IO_DATA9, EXAMPLE_LCD_RGB_IO_DATA10, EXAMPLE_LCD_RGB_IO_DATA11,
                EXAMPLE_LCD_RGB_IO_DATA12, EXAMPLE_LCD_RGB_IO_DATA13, EXAMPLE_LCD_RGB_IO_DATA14, EXAMPLE_LCD_RGB_IO_DATA15,
#endif
            },
        },
    };
    LCD::Config lcd_config = {
        .device = LCD::DevicePartialConfig{
            .reset_gpio_num = EXAMPLE_LCD_RST_IO,
            .bits_per_pixel = EXAMPLE_LCD_COLOR_BITS,
        },
        .vendor = LCD::VendorPartialConfig{
            .hor_res = EXAMPLE_LCD_WIDTH,
            .ver_res = EXAMPLE_LCD_HEIGHT,
        },
    };

    /**
     * Take `ST7262` as an example, the following is the actual code after macro expansion:
     *      LCD_ST7262(bus_config, lcd_config);
     */
    return new EXAMPLE_LCD_CLASS(EXAMPLE_LCD_NAME, bus_config, lcd_config);
}

#if EXAMPLE_LCD_ENABLE_PRINT_FPS

DRAM_ATTR int frame_count = 0;
DRAM_ATTR int fps = 0;
DRAM_ATTR long start_time = 0;

IRAM_ATTR bool onLCD_RefreshFinishCallback(void *user_data)
{
    if (start_time == 0) {
        start_time = millis();

        return false;
    }

    frame_count++;
    if (frame_count >= EXAMPLE_LCD_PRINT_FPS_COUNT_MAX) {
        fps = EXAMPLE_LCD_PRINT_FPS_COUNT_MAX * 1000 / (millis() - start_time);
        esp_rom_printf("LCD FPS: %d\n", fps);
        frame_count = 0;
        start_time = millis();
    }

    return false;
}
#endif // EXAMPLE_LCD_ENABLE_PRINT_FPS

#if EXAMPLE_LCD_ENABLE_DRAW_FINISH_CALLBACK
IRAM_ATTR bool onLCD_DrawFinishCallback(void *user_data)
{
    esp_rom_printf("LCD draw finish callback\n");

    return false;
}
#endif

void waveshare_display_init(void)
{
    Serial.println("Initializing WaveShare ESP32-S3 Touch LCD 7\" board");
    
    // Create board instance with supported configuration
    Board *board = new Board();
    
    // Initialize the board - this will load the WaveShare ESP32-S3 Touch LCD 7" configuration
    if (!board->init()) {
        Serial.println("ERROR: Failed to initialize board");
        return;
    }
    
    // Start the board - this initializes all devices (LCD, Touch, etc.)
    if (!board->begin()) {
        Serial.println("ERROR: Failed to start board");
        return;
    }
    
    auto lcd = board->getLCD();
    auto touch = board->getTouch();
    
    if (lcd == nullptr) {
        Serial.println("ERROR: LCD device not available");
        return;
    }
    
    Serial.println("Board initialized successfully");
    Serial.printf("LCD: %dx%d\n", lcd->getFrameWidth(), lcd->getFrameHeight());
    
    if (touch != nullptr) {
        Serial.println("Touch controller detected and initialized");
    } else {
        Serial.println("No touch controller available");
    }

// #if EXAMPLE_LCD_ENABLE_CREATE_WITH_CONFIG
//     Serial.println("Initializing \"RGB\" LCD with config");
//     auto lcd = create_lcd_with_config();
// #else
//     Serial.println("Initializing \"RGB\" LCD without config");
//     auto lcd = create_lcd_without_config();
// #endif

    // Configure bounce buffer to avoid screen drift
    auto bus = static_cast<BusRGB *>(lcd->getBus());
    bus->configRGB_BounceBufferSize(EXAMPLE_LCD_RGB_BOUNCE_BUFFER_SIZE); // Set bounce buffer to avoid screen drift

    // Note: lcd->init(), lcd->reset(), and lcd->begin() are already called by board->begin()
    // No need to call them again

    // Initialize LVGL with LCD and Touch
    lvgl_port_init(lcd, touch);

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
    //     label_2, "ESP32_Display_Panel (%d.%d.%d)",
    //     ESP_PANEL_VERSION_MAJOR, ESP_PANEL_VERSION_MINOR, ESP_PANEL_VERSION_PATCH
    // );
    // lv_obj_set_style_text_font(label_2, &lv_font_montserrat_14, 0);
    // lv_obj_align_to(label_2, label_1, LV_ALIGN_OUT_BOTTOM_MID, 0, 10);
    // lv_obj_t *label_3 = lv_label_create(lv_scr_act());
    // lv_label_set_text_fmt(label_3, "LVGL (%d.%d.%d)", LVGL_VERSION_MAJOR, LVGL_VERSION_MINOR, LVGL_VERSION_PATCH);
    // lv_obj_set_style_text_font(label_3, &lv_font_montserrat_14, 0);
    // lv_obj_align_to(label_3, label_2, LV_ALIGN_OUT_BOTTOM_MID, 0, 10);

    ui_init();
    /* Release the mutex */
    lvgl_port_unlock();
}