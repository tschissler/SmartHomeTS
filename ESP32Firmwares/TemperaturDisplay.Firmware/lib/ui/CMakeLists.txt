SET(SOURCES ui_Screen1.c
    ui.c
    ui_comp_hook.c
    ui_helpers.c
    fonts/ui_font_Quantico_20.c
    fonts/ui_font_Quantico_32.c
    fonts/ui_font_Quantico_40.c
    fonts/ui_font_Quantico_60.c
    fonts/ui_font_Quantico_80.c
    fonts/ui_font_Quantico_100.c)

add_library(ui ${SOURCES})
