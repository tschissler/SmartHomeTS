/*
 * SPDX-FileCopyrightText: 2024-2025 Espressif Systems (Shanghai) CO LTD
 *
 * SPDX-License-Identifier: CC0-1.0
 */

#include "freertos/FreeRTOS.h"

#include "esp_timer.h"
// #undef ESP_UTILS_LOG_TAG
// #define ESP_UTILS_LOG_TAG "LvPort"
#define TAG "LvPort"
#include "esp_lib_utils.h"
#include "lvgl_v9_port.h"

using namespace esp_panel::drivers;

#define LVGL_PORT_ENABLE_ROTATION_OPTIMIZED     (1)
#define LVGL_PORT_BUFFER_NUM_MAX                (2)

static void flush_callback(lv_display_t *display, const lv_area_t *area, uint8_t *color_map)
{
    LCD *lcd = (LCD *)lv_display_get_driver_data(display);
    const int offsetx1 = area->x1;
    const int offsetx2 = area->x2;
    const int offsety1 = area->y1;
    const int offsety2 = area->y2;

    lcd->drawBitmap(offsetx1, offsety1, offsetx2 - offsetx1 + 1, offsety2 - offsety1 + 1, (const uint8_t *)color_map);
    // For RGB LCD, directly notify LVGL that the buffer is ready
    if (lcd->getBus()->getBasicAttributes().type == ESP_PANEL_BUS_TYPE_RGB) {
        lv_display_flush_ready(display);
    }
}

#include "freertos/FreeRTOS.h"
#include "freertos/semphr.h"
#include <stdlib.h>
#include <assert.h>
#include <memory>
#include <iostream>
#include "esp_attr.h"
#include "esp_log.h"
#include "esp_check.h"
#include "esp_heap_caps.h"
#include <esp_display_panel.hpp>
#include <esp_io_expander.hpp>
// #include "esp_utils.h"

static SemaphoreHandle_t lvgl_mux = nullptr;  // LVGL mutex
static TaskHandle_t lvgl_task_handle = nullptr;
static esp_timer_handle_t lvgl_tick_timer = NULL;
static void *lvgl_buf[LVGL_PORT_BUFFER_NUM_MAX] = {};

#if LVGL_PORT_ROTATION_DEGREE != 0
static void *get_next_frame_buffer(LCD *lcd)
{
    static void *next_fb = NULL;
    static void *fbs[2] = { NULL };

    if (next_fb == NULL) {
        fbs[0] = lcd->getFrameBufferByIndex(0);
        fbs[1] = lcd->getFrameBufferByIndex(1);
        next_fb = fbs[1];
    } else {
        next_fb = (next_fb == fbs[0]) ? fbs[1] : fbs[0];
    }

    return next_fb;
}

__attribute__((always_inline))
static inline void copy_pixel_8bpp(uint8_t *to, const uint8_t *from)
{
    *to++ = *from++;
}

__attribute__((always_inline))
static inline void copy_pixel_16bpp(uint8_t *to, const uint8_t *from)
{
    *(uint16_t *)to++ = *(const uint16_t *)from++;
}

__attribute__((always_inline))
static inline void copy_pixel_24bpp(uint8_t *to, const uint8_t *from)
{
    *to++ = *from++;
    *to++ = *from++;
    *to++ = *from++;
}

#define _COPY_PIXEL(_bpp, to, from) copy_pixel_##_bpp##bpp(to, from)
#define COPY_PIXEL(_bpp, to, from)  _COPY_PIXEL(_bpp, to, from)

#define ROTATE_90_ALL_BPP() \
    { \
        to_bytes_per_line = h * to_bytes_per_piexl; \
        to_index_const = (w - x_start - 1) * to_bytes_per_line; \
        for (int from_y = y_start; from_y < y_end + 1; from_y++) { \
            from_index = from_y * from_bytes_per_line + x_start * from_bytes_per_piexl; \
            to_index = to_index_const + from_y * to_bytes_per_piexl; \
            for (int from_x = x_start; from_x < x_end + 1; from_x++) { \
                COPY_PIXEL(LV_COLOR_DEPTH, to + to_index, from + from_index); \
                from_index += from_bytes_per_piexl; \
                to_index -= to_bytes_per_line; \
            } \
        } \
    }

/**
 * @brief Optimized transpose function for RGB565 format.
 *
 * @note  ESP32-P4 1024x600 full-screen: 738ms -> 34ms
 * @note  ESP32-S3 480x480  full-screen: 380ms -> 37ms
 */
#define ROTATE_90_OPTIMIZED_16BPP(block_w, block_h) \
    { \
        for (int i = 0; i < h; i += block_h) { \
            max_height = (i + block_h > h) ? h : (i + block_h); \
            for (int j = 0; j < w; j += block_w) { \
                max_width = (j + block_w > w) ? w : (j + block_w); \
                start_y = w - 1 - j;   \
                for (int x = i; x < max_height; x++) { \
                    from_next = (uint16_t *)from + x * w; \
                    for (int y = j, mirrored_y = start_y; y < max_width; y += 4, mirrored_y -= 4) { \
                        ((uint16_t *)to)[(mirrored_y) * h + x] = *((uint32_t *)(from_next + y)) & 0xFFFF; \
                        ((uint16_t *)to)[(mirrored_y - 1) * h + x] = (*((uint32_t *)(from_next + y)) >> 16) & 0xFFFF; \
                        ((uint16_t *)to)[(mirrored_y - 2) * h + x] = *((uint32_t *)(from_next + y + 2)) & 0xFFFF; \
                        ((uint16_t *)to)[(mirrored_y - 3) * h + x] = (*((uint32_t *)(from_next + y + 2)) >> 16) & 0xFFFF; \
                    } \
                } \
            } \
        } \
    }

#define ROTATE_180_ALL_BPP() \
    { \
        to_bytes_per_line = w * to_bytes_per_piexl; \
        to_index_const = (h - 1) * to_bytes_per_line + (w - x_start - 1) * to_bytes_per_piexl; \
        for (int from_y = y_start; from_y < y_end + 1; from_y++) { \
            from_index = from_y * from_bytes_per_line + x_start * from_bytes_per_piexl; \
            to_index = to_index_const - from_y * to_bytes_per_line; \
            for (int from_x = x_start; from_x < x_end + 1; from_x++) { \
                COPY_PIXEL(LV_COLOR_DEPTH, to + to_index, from + from_index); \
                from_index += from_bytes_per_piexl; \
                to_index -= to_bytes_per_piexl; \
            } \
        } \
    }

#define ROTATE_270_OPTIMIZED_16BPP(block_w, block_h) \
    { \
        for (int i = 0; i < h; i += block_h) { \
            max_height = i + block_h > h ? h : i + block_h; \
            for (int j = 0; j < w; j += block_w) { \
                max_width = j + block_w > w ? w : j + block_w; \
                for (int x = i; x < max_height; x++) { \
                    from_next = (uint16_t *)from + x * w; \
                    for (int y = j; y < max_width; y += 4) { \
                        ((uint16_t *)to)[y * h + (h - 1 - x)] = *((uint32_t *)(from_next + y)) & 0xFFFF; \
                        ((uint16_t *)to)[(y + 1) * h + (h - 1 - x)] = (*((uint32_t *)(from_next + y)) >> 16) & 0xFFFF; \
                        ((uint16_t *)to)[(y + 2) * h + (h - 1 - x)] = *((uint32_t *)(from_next + y + 2)) & 0xFFFF; \
                        ((uint16_t *)to)[(y + 3) * h + (h - 1 - x)] = (*((uint32_t *)(from_next + y + 2)) >> 16) & 0xFFFF; \
                    } \
                } \
            } \
        } \
    }

#define ROTATE_270_ALL_BPP() \
    { \
        to_bytes_per_line = h * to_bytes_per_piexl; \
        from_index_const = x_start * from_bytes_per_piexl; \
        to_index_const = x_start * to_bytes_per_line + (h - 1) * to_bytes_per_piexl; \
        for (int from_y = y_start; from_y < y_end + 1; from_y++) { \
            from_index = from_y * from_bytes_per_line + from_index_const; \
            to_index = to_index_const - from_y * to_bytes_per_piexl; \
            for (int from_x = x_start; from_x < x_end + 1; from_x++) { \
                COPY_PIXEL(LV_COLOR_DEPTH, to + to_index, from + from_index); \
                from_index += from_bytes_per_piexl; \
                to_index += to_bytes_per_line; \
            } \
        } \
    }

__attribute__((always_inline))
IRAM_ATTR static inline void rotate_copy_pixel(
    const uint8_t *from, uint8_t *to, uint16_t x_start, uint16_t y_start, uint16_t x_end, uint16_t y_end, uint16_t w,
    uint16_t h, uint16_t rotate
)
{
    int from_bytes_per_piexl = sizeof(lv_color_t);
    int from_bytes_per_line = w * from_bytes_per_piexl;
    int from_index = 0;

    int to_bytes_per_piexl = LV_COLOR_DEPTH >> 3;
    int to_bytes_per_line;
    int to_index = 0;
    int to_index_const = 0;

#if (LV_COLOR_DEPTH == 16) && LVGL_PORT_ENABLE_ROTATION_OPTIMIZED
    int max_height = 0;
    int max_width = 0;
    int start_y = 0;
    uint16_t *from_next = NULL;
#endif

    // uint32_t time = esp_log_timestamp();
    switch (rotate) {
    case 90:
#if (LV_COLOR_DEPTH == 16) && LVGL_PORT_ENABLE_ROTATION_OPTIMIZED
        ROTATE_90_OPTIMIZED_16BPP(32, 256);
#else
        ROTATE_90_ALL_BPP();
#endif
        break;
    case 180:
        ROTATE_180_ALL_BPP();
        break;
    case 270:
#if (LV_COLOR_DEPTH == 16) && LVGL_PORT_ENABLE_ROTATION_OPTIMIZED
        ROTATE_270_OPTIMIZED_16BPP(32, 256);
#else
        int from_index_const = 0;
        ROTATE_270_ALL_BPP();
#endif
        break;
    default:
        break;
    }
    // ESP_LOGI(TAG, "rotate: end, time used:%d", (int)(esp_log_timestamp() - time));
}
#endif /* LVGL_PORT_ROTATION_DEGREE */

#if LVGL_PORT_AVOID_TEAR
#if LVGL_PORT_DIRECT_MODE
#if LVGL_PORT_ROTATION_DEGREE != 0
typedef struct {
    uint16_t inv_p;
    uint8_t inv_area_joined[LV_INV_BUF_SIZE];
    lv_area_t inv_areas[LV_INV_BUF_SIZE];
} lv_port_dirty_area_t;

static lv_port_dirty_area_t dirty_area;

static void flush_dirty_save(lv_port_dirty_area_t *dirty_area)
{
    // In LVGL 9, invalidation management is different
    // For now, we'll use a simplified approach
    dirty_area->inv_p = 1;
    dirty_area->inv_area_joined[0] = 0;
    dirty_area->inv_areas[0].x1 = 0;
    dirty_area->inv_areas[0].y1 = 0;
    dirty_area->inv_areas[0].x2 = LV_HOR_RES - 1;
    dirty_area->inv_areas[0].y2 = LV_VER_RES - 1;
}

typedef enum {
    FLUSH_STATUS_PART,
    FLUSH_STATUS_FULL
} lv_port_flush_status_t;

typedef enum {
    FLUSH_PROBE_PART_COPY,
    FLUSH_PROBE_SKIP_COPY,
    FLUSH_PROBE_FULL_COPY,
} lv_port_flush_probe_t;

/**
 * @brief Probe dirty area to copy
 *
 * @note This function is used to avoid tearing effect, and only work with LVGL direct-mode.
 */
static lv_port_flush_probe_t flush_copy_probe(lv_display_t *display)
{
    static lv_port_flush_status_t prev_status = FLUSH_STATUS_PART;
    lv_port_flush_status_t cur_status;
    lv_port_flush_probe_t probe_result;
    
    // In LVGL 9, we need to get invalidation info differently
    // For now, use a simplified approach
    uint32_t flush_ver = lv_display_get_vertical_resolution(display);
    uint32_t flush_hor = lv_display_get_horizontal_resolution(display);
    
    /* Check if the current full screen refreshes */
    cur_status = FLUSH_STATUS_FULL; // Simplified for now

    if (prev_status == FLUSH_STATUS_FULL) {
        if ((cur_status == FLUSH_STATUS_PART)) {
            probe_result = FLUSH_PROBE_FULL_COPY;
        } else {
            probe_result = FLUSH_PROBE_SKIP_COPY;
        }
    } else {
        probe_result = FLUSH_PROBE_PART_COPY;
    }
    prev_status = cur_status;

    return probe_result;
}

static inline void *flush_get_next_buf(LCD *lcd)
{
    return get_next_frame_buffer(lcd);
}

/**
 * @brief Copy dirty area
 *
 * @note This function is used to avoid tearing effect, and only work with LVGL direct-mode.
 */
static void flush_dirty_copy(void *dst, void *src, lv_port_dirty_area_t *dirty_area)
{
    lv_coord_t x_start, x_end, y_start, y_end;
    for (int i = 0; i < dirty_area->inv_p; i++) {
        /* Refresh the unjoined areas*/
        if (dirty_area->inv_area_joined[i] == 0) {
            x_start = dirty_area->inv_areas[i].x1;
            x_end = dirty_area->inv_areas[i].x2;
            y_start = dirty_area->inv_areas[i].y1;
            y_end = dirty_area->inv_areas[i].y2;

            rotate_copy_pixel(
                (uint8_t *)src, (uint8_t *)dst, x_start, y_start, x_end, y_end, LV_HOR_RES, LV_VER_RES,
                LVGL_PORT_ROTATION_DEGREE
            );
        }
    }
}

static void flush_callback(lv_display_t *display, const lv_area_t *area, uint8_t *color_map)
{
    LCD *lcd = (LCD *)lv_display_get_driver_data(display);
    const int offsetx1 = area->x1;
    const int offsetx2 = area->x2;
    const int offsety1 = area->y1;
    const int offsety2 = area->y2;
    void *next_fb = NULL;
    lv_port_flush_probe_t probe_result = FLUSH_PROBE_PART_COPY;

    /* Action after last area refresh */
    if (lv_display_flush_is_last(display)) {
        /* Check if the `full_refresh` flag has been triggered */
        if (lv_display_get_render_mode(display) == LV_DISPLAY_RENDER_MODE_FULL) {
            // Rotate and copy data from the whole screen LVGL's buffer to the next frame buffer
            next_fb = flush_get_next_buf(lcd);
            rotate_copy_pixel(
                (uint8_t *)color_map, (uint8_t *)next_fb, offsetx1, offsety1, offsetx2, offsety2,
                LV_HOR_RES, LV_VER_RES, LVGL_PORT_ROTATION_DEGREE
            );

            /* Switch the current LCD frame buffer to `next_fb` */
            lcd->switchFrameBufferTo(next_fb);

            /* Waiting for the current frame buffer to complete transmission */
            ulTaskNotifyValueClear(NULL, ULONG_MAX);
            ulTaskNotifyTake(pdTRUE, portMAX_DELAY);

            /* Synchronously update the dirty area for another frame buffer */
            flush_dirty_copy(flush_get_next_buf(lcd), color_map, &dirty_area);
            flush_get_next_buf(lcd);
        } else {
            /* Probe the copy method for the current dirty area */
            probe_result = flush_copy_probe(display);

            if (probe_result == FLUSH_PROBE_FULL_COPY) {
                /* Save current dirty area for next frame buffer */
                flush_dirty_save(&dirty_area);

                /* Set LVGL full-refresh flag and set flush ready in advance */
                lv_display_set_render_mode(display, LV_DISPLAY_RENDER_MODE_FULL);
                lv_display_flush_ready(display);

                /* Force to refresh whole screen, and will invoke `flush_callback` recursively */
                lv_refr_now(display);
            } else {
                /* Update current dirty area for next frame buffer */
                next_fb = flush_get_next_buf(lcd);
                flush_dirty_save(&dirty_area);
                flush_dirty_copy(next_fb, color_map, &dirty_area);

                /* Switch the current LCD frame buffer to `next_fb` */
                lcd->switchFrameBufferTo(next_fb);

                /* Waiting for the current frame buffer to complete transmission */
                ulTaskNotifyValueClear(NULL, ULONG_MAX);
                ulTaskNotifyTake(pdTRUE, portMAX_DELAY);

                if (probe_result == FLUSH_PROBE_PART_COPY) {
                    /* Synchronously update the dirty area for another frame buffer */
                    flush_dirty_save(&dirty_area);
                    flush_dirty_copy(flush_get_next_buf(lcd), color_map, &dirty_area);
                    flush_get_next_buf(lcd);
                }
            }
        }
    }

    lv_display_flush_ready(display);
}

#else

static void flush_callback(lv_display_t *display, const lv_area_t *area, uint8_t *color_map)
{
    LCD *lcd = (LCD *)lv_display_get_driver_data(display);

    /* Action after last area refresh */
    if (lv_display_flush_is_last(display)) {
        /* Switch the current LCD frame buffer to `color_map` */
        lcd->switchFrameBufferTo(color_map);

        /* Waiting for the last frame buffer to complete transmission */
        ulTaskNotifyValueClear(NULL, ULONG_MAX);
        ulTaskNotifyTake(pdTRUE, portMAX_DELAY);
    }

    lv_display_flush_ready(display);
}
#endif /* LVGL_PORT_ROTATION_DEGREE */

#elif LVGL_PORT_FULL_REFRESH && LVGL_PORT_DISP_BUFFER_NUM == 2

static void flush_callback(lv_display_t *display, const lv_area_t *area, uint8_t *color_map)
{
    LCD *lcd = (LCD *)lv_display_get_driver_data(display);

    /* Switch the current LCD frame buffer to `color_map` */
    lcd->switchFrameBufferTo(color_map);

    /* Waiting for the last frame buffer to complete transmission */
    ulTaskNotifyValueClear(NULL, ULONG_MAX);
    ulTaskNotifyTake(pdTRUE, portMAX_DELAY);

    lv_display_flush_ready(display);
}

#elif LVGL_PORT_FULL_REFRESH && LVGL_PORT_DISP_BUFFER_NUM == 3

#if LVGL_PORT_ROTATION_DEGREE == 0
static void *lvgl_port_lcd_last_buf = NULL;
static void *lvgl_port_lcd_next_buf = NULL;
static void *lvgl_port_flush_next_buf = NULL;
#endif

static void flush_callback(lv_display_t *display, const lv_area_t *area, uint8_t *color_map)
{
    LCD *lcd = (LCD *)lv_display_get_driver_data(display);

#if LVGL_PORT_ROTATION_DEGREE != 0
    const int offsetx1 = area->x1;
    const int offsetx2 = area->x2;
    const int offsety1 = area->y1;
    const int offsety2 = area->y2;
    void *next_fb = get_next_frame_buffer(lcd);

    /* Rotate and copy dirty area from the current LVGL's buffer to the next LCD frame buffer */
    rotate_copy_pixel(
        (uint8_t *)color_map, (uint8_t *)next_fb, offsetx1, offsety1, offsetx2, offsety2, LV_HOR_RES,
        LV_VER_RES, LVGL_PORT_ROTATION_DEGREE
    );

    /* Switch the current LCD frame buffer to `next_fb` */
    lcd->switchFrameBufferTo(next_fb);
#else
    // Note: In LVGL 9, draw buffer management is handled differently
    // We'll need to adapt this based on the new display buffer API
    lvgl_port_flush_next_buf = color_map;

    /* Switch the current LCD frame buffer to `color_map` */
    lcd->switchFrameBufferTo(color_map);

    lvgl_port_lcd_next_buf = color_map;
#endif

    lv_display_flush_ready(display);
}
#endif

IRAM_ATTR bool onLcdVsyncCallback(void *user_data)
{
    BaseType_t need_yield = pdFALSE;
#if LVGL_PORT_FULL_REFRESH && (LVGL_PORT_DISP_BUFFER_NUM == 3) && (LVGL_PORT_ROTATION_DEGREE == 0)
    if (lvgl_port_lcd_next_buf != lvgl_port_lcd_last_buf) {
        lvgl_port_flush_next_buf = lvgl_port_lcd_last_buf;
        lvgl_port_lcd_last_buf = lvgl_port_lcd_next_buf;
    }
#else
    TaskHandle_t task_handle = (TaskHandle_t)user_data;
    // Notify that the current LCD frame buffer has been transmitted
    xTaskNotifyFromISR(task_handle, ULONG_MAX, eNoAction, &need_yield);
#endif
    return (need_yield == pdTRUE);
}

#else

static void update_callback(lv_event_t * e)
{
    lv_display_t *display = (lv_display_t *)lv_event_get_target(e);
    LCD *lcd = (LCD *)lv_event_get_user_data(e);
    auto transformation = lcd->getTransformation();
    static bool disp_init_mirror_x = transformation.mirror_x;
    static bool disp_init_mirror_y = transformation.mirror_y;
    static bool disp_init_swap_xy = transformation.swap_xy;

    lv_display_rotation_t rotation = lv_display_get_rotation(display);
    
    switch (rotation) {
    case LV_DISPLAY_ROTATION_0:
        lcd->swapXY(disp_init_swap_xy);
        lcd->mirrorX(disp_init_mirror_x);
        lcd->mirrorY(disp_init_mirror_y);
        break;
    case LV_DISPLAY_ROTATION_90:
        lcd->swapXY(!disp_init_swap_xy);
        lcd->mirrorX(disp_init_mirror_x);
        lcd->mirrorY(!disp_init_mirror_y);
        break;
    case LV_DISPLAY_ROTATION_180:
        lcd->swapXY(disp_init_swap_xy);
        lcd->mirrorX(!disp_init_mirror_x);
        lcd->mirrorY(!disp_init_mirror_y);
        break;
    case LV_DISPLAY_ROTATION_270:
        lcd->swapXY(!disp_init_swap_xy);
        lcd->mirrorX(!disp_init_mirror_x);
        lcd->mirrorY(disp_init_mirror_y);
        break;
    }

    ESP_LOGD(TAG, "Update display rotation to %d", rotation);

#if CONFIG_LOG_DEFAULT_LEVEL >= 4 // ESP_LOG_DEBUG
    transformation = lcd->getTransformation();
    disp_init_mirror_x = transformation.mirror_x;
    disp_init_mirror_y = transformation.mirror_y;
    disp_init_swap_xy = transformation.swap_xy;
    ESP_LOGD(TAG, "Current mirror x: %d, mirror y: %d, swap xy: %d", disp_init_mirror_x, disp_init_mirror_y, disp_init_swap_xy);
#endif
}

#endif /* LVGL_PORT_AVOID_TEAR */

void rounder_callback(lv_event_t * e)
{
    // Note: Rounder callbacks work differently in LVGL 9
    // This function may need to be refactored or removed
    // For now, we'll keep the same logic but with event parameters
    lv_display_t *display = (lv_display_t *)lv_event_get_target(e);
    LCD *lcd = (LCD *)lv_event_get_user_data(e);
    
    // In LVGL 9, area rounding is handled differently
    // This function might not be called in the same way
    uint8_t x_align = lcd->getBasicAttributes().basic_bus_spec.x_coord_align;
    uint8_t y_align = lcd->getBasicAttributes().basic_bus_spec.y_coord_align;
    
    // TODO: Implement proper area rounding for LVGL 9 if needed
}

static lv_display_t * display_init(LCD *lcd)
{
    if (lcd == nullptr) {
        ESP_LOGE(TAG, "Invalid LCD device");
        return nullptr;
    }
    if (lcd->getRefreshPanelHandle() == nullptr) {
        ESP_LOGE(TAG, "LCD device is not initialized");
        return nullptr;
    }

    // Alloc draw buffers used by LVGL
    auto lcd_width = lcd->getFrameWidth();
    auto lcd_height = lcd->getFrameHeight();
    int buffer_size = 0;

    ESP_LOGD(TAG, "Malloc memory for LVGL buffer");
#if !LVGL_PORT_AVOID_TEAR
    // Avoid tearing function is disabled
    buffer_size = lcd_width * LVGL_PORT_BUFFER_SIZE_HEIGHT;
    for (int i = 0; (i < LVGL_PORT_BUFFER_NUM) && (i < LVGL_PORT_BUFFER_NUM_MAX); i++) {
        lvgl_buf[i] = heap_caps_malloc(buffer_size * sizeof(lv_color_t), LVGL_PORT_BUFFER_MALLOC_CAPS);
        assert(lvgl_buf[i]);
        ESP_LOGD(TAG, "Buffer[%d] address: %p, size: %d", i, lvgl_buf[i], buffer_size * sizeof(lv_color_t));
    }
#else
    // To avoid the tearing effect, we should use at least two frame buffers: one for LVGL rendering and another for LCD refresh
    buffer_size = lcd_width * lcd_height;
#if (LVGL_PORT_DISP_BUFFER_NUM >= 3) && (LVGL_PORT_ROTATION_DEGREE == 0) && LVGL_PORT_FULL_REFRESH

    // With the usage of three buffers and full-refresh, we always have one buffer available for rendering,
    // eliminating the need to wait for the LCD's sync signal
    lvgl_port_lcd_last_buf = lcd->getFrameBufferByIndex(0);
    lvgl_buf[0] = lcd->getFrameBufferByIndex(1);
    lvgl_buf[1] = lcd->getFrameBufferByIndex(2);
    lvgl_port_lcd_next_buf = lvgl_port_lcd_last_buf;
    lvgl_port_flush_next_buf = lvgl_buf[1];

#elif (LVGL_PORT_DISP_BUFFER_NUM >= 3) && (LVGL_PORT_ROTATION_DEGREE != 0)

    lvgl_buf[0] = lcd->getFrameBufferByIndex(2);

#elif LVGL_PORT_DISP_BUFFER_NUM >= 2

    for (int i = 0; (i < LVGL_PORT_DISP_BUFFER_NUM) && (i < LVGL_PORT_BUFFER_NUM_MAX); i++) {
        lvgl_buf[i] = lcd->getFrameBufferByIndex(i);
    }

#endif
#endif /* LVGL_PORT_AVOID_TEAR */

    ESP_LOGD(TAG, "Register display driver to LVGL");
    
    // Create display instead of initializing driver
    lv_display_t * disp = lv_display_create(
#if (LVGL_PORT_ROTATION_DEGREE == 90) || (LVGL_PORT_ROTATION_DEGREE == 270)
        lcd_height, lcd_width
#else
        lcd_width, lcd_height
#endif
    );
    
    if (!disp) {
        ESP_LOGE(TAG, "Failed to create LVGL display");
        return nullptr;
    }
    
    // Set display properties
    lv_display_set_flush_cb(disp, flush_callback);
    
    // Initialize LVGL draw buffers using the simpler approach for LVGL 9
    // Use lv_display_set_buffers which is the recommended way in LVGL 9
    lv_display_set_buffers(disp, lvgl_buf[0], lvgl_buf[1], buffer_size, LV_DISPLAY_RENDER_MODE_PARTIAL);

#if LVGL_PORT_AVOID_TEAR    // Only available when the tearing effect is enabled
#if LVGL_PORT_FULL_REFRESH
    lv_display_set_render_mode(disp, LV_DISPLAY_RENDER_MODE_FULL);
#elif LVGL_PORT_DIRECT_MODE
    lv_display_set_render_mode(disp, LV_DISPLAY_RENDER_MODE_DIRECT);
#endif
#else                       // Only available when the tearing effect is disabled
    if (lcd->getBasicAttributes().basic_bus_spec.isFunctionValid(LCD::BasicBusSpecification::FUNC_SWAP_XY) &&
            lcd->getBasicAttributes().basic_bus_spec.isFunctionValid(LCD::BasicBusSpecification::FUNC_MIRROR_X) &&
            lcd->getBasicAttributes().basic_bus_spec.isFunctionValid(LCD::BasicBusSpecification::FUNC_MIRROR_Y)) {
        lv_display_add_event_cb(disp, update_callback, LV_EVENT_RESOLUTION_CHANGED, (void*)lcd);
    } else {
        // Set rotation if needed
#if LVGL_PORT_ROTATION_DEGREE != 0
        lv_display_set_rotation(disp, (lv_display_rotation_t)(LVGL_PORT_ROTATION_DEGREE / 90));
#endif
    }
#endif /* LVGL_PORT_AVOID_TEAR */

    lv_display_set_driver_data(disp, (void *)lcd);
    
    // Only available when the coordinate alignment is enabled
    if ((lcd->getBasicAttributes().basic_bus_spec.x_coord_align > 1) ||
            (lcd->getBasicAttributes().basic_bus_spec.y_coord_align > 1)) {
        // Note: rounder callback might need adjustment for LVGL 9
    }

    return disp;
}

static SemaphoreHandle_t touch_detected;

static void touchpad_read(lv_indev_t *indev, lv_indev_data_t *data)
{
    Touch *tp = (Touch *)lv_indev_get_driver_data(indev);
    TouchPoint point;
    data->state = LV_INDEV_STATE_RELEASED;

    /* if we are interrupt driven wait for the ISR to fire */
    if ( tp->isInterruptEnabled() && (xSemaphoreTake( touch_detected, 0 ) == pdFALSE) ) {
        return;
    }

    /* Read data from touch controller */
    int read_touch_result = tp->readPoints(&point, 1, 0);
    if (read_touch_result > 0) {
        data->point.x = point.x;
        data->point.y = point.y;
        data->state = LV_INDEV_STATE_PRESSED;
    }
}

static bool onTouchInterruptCallback(void *user_data)
{
    BaseType_t xHigherPriorityTaskWoken = pdFALSE;
    xSemaphoreGiveFromISR( touch_detected, &xHigherPriorityTaskWoken );
    portYIELD_FROM_ISR( xHigherPriorityTaskWoken );
    return false;
}

static lv_indev_t *indev_init(Touch *tp)
{
    if (tp == nullptr) {
        ESP_LOGE(TAG, "Invalid touch device");
        return nullptr;
    }
    if (tp->getPanelHandle() == nullptr) {
        ESP_LOGE(TAG, "Touch device is not initialized");
        return nullptr;
    }

    if (tp->isInterruptEnabled()) {
        touch_detected = xSemaphoreCreateBinary();
        tp->attachInterruptCallback(onTouchInterruptCallback, tp);
    }
    ESP_LOGD(TAG, "Register input driver to LVGL");
    
    lv_indev_t *indev = lv_indev_create();
    lv_indev_set_type(indev, LV_INDEV_TYPE_POINTER);
    lv_indev_set_read_cb(indev, touchpad_read);
    lv_indev_set_driver_data(indev, (void *)tp);

    return indev;
}

#if !LV_TICK_CUSTOM
static void tick_increment(void *arg)
{
    /* Tell LVGL how many milliseconds have elapsed */
    lv_tick_inc(LVGL_PORT_TICK_PERIOD_MS);
}

static bool tick_init(void)
{
    // Tick interface for LVGL (using esp_timer to generate 2ms periodic event)
    const esp_timer_create_args_t lvgl_tick_timer_args = {
        .callback = &tick_increment,
        .name = "LVGL tick"
    };
    esp_err_t ret = esp_timer_create(&lvgl_tick_timer_args, &lvgl_tick_timer);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Create LVGL tick timer failed");
        return false;
    }
    ret = esp_timer_start_periodic(lvgl_tick_timer, LVGL_PORT_TICK_PERIOD_MS * 1000);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Start LVGL tick timer failed");
        return false;
    }

    return true;
}

static bool tick_deinit(void)
{
    esp_err_t ret = esp_timer_stop(lvgl_tick_timer);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Stop LVGL tick timer failed");
        return false;
    }
    ret = esp_timer_delete(lvgl_tick_timer);
    if (ret != ESP_OK) {
        ESP_LOGE(TAG, "Delete LVGL tick timer failed");
        return false;
    }
    return true;
}
#endif

static void lvgl_port_task(void *arg)
{
    ESP_LOGD(TAG, "Starting LVGL task");

    uint32_t task_delay_ms = LVGL_PORT_TASK_MAX_DELAY_MS;
    while (1) {
        if (lvgl_port_lock(-1)) {
            task_delay_ms = lv_timer_handler();
            lvgl_port_unlock();
        }
        if (task_delay_ms > LVGL_PORT_TASK_MAX_DELAY_MS) {
            task_delay_ms = LVGL_PORT_TASK_MAX_DELAY_MS;
        } else if (task_delay_ms < LVGL_PORT_TASK_MIN_DELAY_MS) {
            task_delay_ms = LVGL_PORT_TASK_MIN_DELAY_MS;
        }
        vTaskDelay(pdMS_TO_TICKS(task_delay_ms));
    }
}

IRAM_ATTR bool onDrawBitmapFinishCallback(void *user_data)
{
    lv_display_t *display = (lv_display_t *)user_data;

    lv_display_flush_ready(display);

    return false;
}

bool lvgl_port_init(LCD *lcd, Touch *tp)
{
    ESP_UTILS_CHECK_FALSE_RETURN(lcd != nullptr, false, "Invalid LCD device");

    auto bus_type = lcd->getBus()->getBasicAttributes().type;
#if LVGL_PORT_AVOID_TEAR
    ESP_UTILS_CHECK_FALSE_RETURN(
        (bus_type == ESP_PANEL_BUS_TYPE_RGB) || (bus_type == ESP_PANEL_BUS_TYPE_MIPI_DSI), false,
        "Avoid tearing function only works with RGB/MIPI-DSI LCD now"
    );
    ESP_UTILS_LOGI(
        "Avoid tearing is enabled, mode: %d, rotation: %d", LVGL_PORT_AVOID_TEARING_MODE, LVGL_PORT_ROTATION_DEGREE
    );
#endif

    lv_display_t *disp = nullptr;
    lv_indev_t *indev = nullptr;

    lv_init();
#if !LV_TICK_CUSTOM
    ESP_UTILS_CHECK_FALSE_RETURN(tick_init(), false, "Initialize LVGL tick failed");
#endif

    ESP_UTILS_LOGI("Initializing LVGL display driver");
    disp = display_init(lcd);
    ESP_UTILS_CHECK_NULL_RETURN(disp, false, "Initialize LVGL display driver failed");
    // Record the initial rotation of the display
    lv_display_set_rotation(disp, LV_DISPLAY_ROTATION_0);

    // For non-RGB LCD, need to notify LVGL that the buffer is ready when the refresh is finished
    if (bus_type != ESP_PANEL_BUS_TYPE_RGB) {
        ESP_UTILS_LOGD("Attach refresh finish callback to LCD");
        lcd->attachDrawBitmapFinishCallback(onDrawBitmapFinishCallback, (void *)disp);
    }

    if (tp != nullptr) {
        ESP_UTILS_LOGD("Initialize LVGL input driver");
        indev = indev_init(tp);
        ESP_UTILS_CHECK_NULL_RETURN(indev, false, "Initialize LVGL input driver failed");

#if LVGL_PORT_ROTATION_DEGREE != 0
        auto &transformation = tp->getTransformation();
#if LVGL_PORT_ROTATION_DEGREE == 90
        tp->swapXY(!transformation.swap_xy);
        tp->mirrorY(!transformation.mirror_y);
#elif LVGL_PORT_ROTATION_DEGREE == 180
        tp->mirrorX(!transformation.mirror_x);
        tp->mirrorY(!transformation.mirror_y);
#elif LVGL_PORT_ROTATION_DEGREE == 270
        tp->swapXY(!transformation.swap_xy);
        tp->mirrorX(!transformation.mirror_x);
#endif
#endif
    }

    ESP_UTILS_LOGD("Create mutex for LVGL");
    lvgl_mux = xSemaphoreCreateRecursiveMutex();
    ESP_UTILS_CHECK_NULL_RETURN(lvgl_mux, false, "Create LVGL mutex failed");

    ESP_UTILS_LOGD("Create LVGL task");
    BaseType_t core_id = (LVGL_PORT_TASK_CORE < 0) ? tskNO_AFFINITY : LVGL_PORT_TASK_CORE;
    BaseType_t ret = xTaskCreatePinnedToCore(lvgl_port_task, "lvgl", LVGL_PORT_TASK_STACK_SIZE, NULL,
                     LVGL_PORT_TASK_PRIORITY, &lvgl_task_handle, core_id);
    ESP_UTILS_CHECK_FALSE_RETURN(ret == pdPASS, false, "Create LVGL task failed");

#if LVGL_PORT_AVOID_TEAR
    lcd->attachRefreshFinishCallback(onLcdVsyncCallback, (void *)lvgl_task_handle);
#endif

    return true;
}

bool lvgl_port_lock(int timeout_ms)
{
    ESP_UTILS_CHECK_NULL_RETURN(lvgl_mux, false, "LVGL mutex is not initialized");

    const TickType_t timeout_ticks = (timeout_ms < 0) ? portMAX_DELAY : pdMS_TO_TICKS(timeout_ms);
    return (xSemaphoreTakeRecursive(lvgl_mux, timeout_ticks) == pdTRUE);
}

bool lvgl_port_unlock(void)
{
    ESP_UTILS_CHECK_NULL_RETURN(lvgl_mux, false, "LVGL mutex is not initialized");

    xSemaphoreGiveRecursive(lvgl_mux);

    return true;
}

bool lvgl_port_deinit(void)
{
#if !LV_TICK_CUSTOM
    ESP_UTILS_CHECK_FALSE_RETURN(tick_deinit(), false, "Deinitialize LVGL tick failed");
#endif

    ESP_UTILS_CHECK_FALSE_RETURN(lvgl_port_lock(-1), false, "Lock LVGL failed");
    if (lvgl_task_handle != nullptr) {
        vTaskDelete(lvgl_task_handle);
        lvgl_task_handle = nullptr;
    }
    ESP_UTILS_CHECK_FALSE_RETURN(lvgl_port_unlock(), false, "Unlock LVGL failed");

#if LV_ENABLE_GC || !LV_MEM_CUSTOM
    lv_deinit();
#else
    ESP_UTILS_LOGW("LVGL memory is custom, `lv_deinit()` will not work");
#endif
#if !LVGL_PORT_AVOID_TEAR
    for (int i = 0; i < LVGL_PORT_BUFFER_NUM; i++) {
        if (lvgl_buf[i] != nullptr) {
            free(lvgl_buf[i]);
            lvgl_buf[i] = nullptr;
        }
    }
#endif
    if (lvgl_mux != nullptr) {
        vSemaphoreDelete(lvgl_mux);
        lvgl_mux = nullptr;
    }

    return true;
}
