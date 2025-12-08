SET(SOURCES ui_mainScreen.c
    ui_theme_manager.cpp
    ui_themes.cpp
    ui.c
    ui_comp_hook.c
    ui_helpers.c
    ui_img_456559396.c
    ui_img_308378252.c
    ui_img_1385145727.c
    ui_img_859317218.c
    ui_img_valve_png.c
    ui_img_export_png.c
    ui_img_cancel_png.c
    fonts/ui_font_Quantico_100.c
    fonts/ui_font_Quantico_14.c
    fonts/ui_font_Quantico_16.c
    fonts/ui_font_Quantico_20.c
    fonts/ui_font_Quantico_32.c
    fonts/ui_font_Quantico_40.c
    fonts/ui_font_Quantico_60.c
    fonts/ui_font_Quantico_80.c)

add_library(ui ${SOURCES})
