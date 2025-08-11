/*******************************************************************************
 * Size: 16 px
 * Bpp: 1
 * Opts: --bpp 1 --size 16 --font C:/Users/ThomasSchissler/SquareLine/assets/Quantico-Bold.ttf -o C:/Users/ThomasSchissler/SquareLine/assets\ui_font_Quantico_16.c --format lvgl -r 0x20-0x7f --no-compress --no-prefilter
 ******************************************************************************/

#include "ui.h"

#ifndef UI_FONT_QUANTICO_16
#define UI_FONT_QUANTICO_16 1
#endif

#if UI_FONT_QUANTICO_16

/*-----------------
 *    BITMAPS
 *----------------*/

/*Store the image of the glyphs*/
static LV_ATTRIBUTE_LARGE_CONST const uint8_t glyph_bitmap[] = {
    /* U+0020 " " */
    0x0,

    /* U+0021 "!" */
    0x6d, 0xb6, 0xd8, 0x1f, 0xf0,

    /* U+0022 "\"" */
    0xcf, 0x3c, 0xf3, 0xcc,

    /* U+0023 "#" */
    0x33, 0xc, 0xc3, 0x33, 0xff, 0xff, 0xcc, 0xc3,
    0x33, 0xff, 0xff, 0xcc, 0x83, 0x60, 0x98,

    /* U+0024 "$" */
    0x18, 0x18, 0x18, 0x7e, 0xff, 0xc3, 0xc0, 0xfe,
    0x7f, 0x3, 0x3, 0xc3, 0xff, 0x7e, 0x18, 0x18,

    /* U+0025 "%" */
    0x78, 0x33, 0xf1, 0x8c, 0xcc, 0x3f, 0x70, 0x79,
    0x80, 0xd, 0xe0, 0x6f, 0xc3, 0xb3, 0xc, 0xcc,
    0x63, 0xf3, 0x7, 0x80,

    /* U+0026 "&" */
    0x7c, 0x7f, 0x31, 0x98, 0x7, 0x9b, 0xdf, 0xd,
    0x86, 0xc3, 0x7f, 0x9f, 0x80,

    /* U+0027 "'" */
    0xff, 0xc0,

    /* U+0028 "(" */
    0x7f, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcf, 0x70,

    /* U+0029 ")" */
    0xef, 0x33, 0x33, 0x33, 0x33, 0x33, 0x3f, 0xe0,

    /* U+002A "*" */
    0x18, 0x18, 0xff, 0xff, 0x3c, 0x3c, 0x66, 0x24,

    /* U+002B "+" */
    0x18, 0x18, 0x18, 0xff, 0xff, 0x18, 0x18, 0x18,

    /* U+002C "," */
    0xff, 0xed, 0x0,

    /* U+002D "-" */
    0xff, 0xc0,

    /* U+002E "." */
    0xff, 0x80,

    /* U+002F "/" */
    0xc, 0x31, 0x86, 0x18, 0xe3, 0xc, 0x71, 0x86,
    0x18,

    /* U+0030 "0" */
    0x7e, 0xff, 0xc3, 0xc3, 0xc3, 0xc3, 0xc3, 0xc3,
    0xc3, 0xff, 0x7e,

    /* U+0031 "1" */
    0x39, 0xf3, 0x60, 0xc1, 0x83, 0x6, 0xc, 0x19,
    0xff, 0xf8,

    /* U+0032 "2" */
    0x7d, 0xff, 0x18, 0x30, 0x6f, 0xff, 0x60, 0xc1,
    0xff, 0xf8,

    /* U+0033 "3" */
    0x7e, 0xff, 0xc3, 0x3, 0x1e, 0x1e, 0x3, 0x3,
    0xc3, 0xff, 0x7e,

    /* U+0034 "4" */
    0x6, 0x7, 0x7, 0x83, 0xc3, 0x63, 0x31, 0x99,
    0xff, 0xff, 0x83, 0x1, 0x80,

    /* U+0035 "5" */
    0xff, 0xff, 0xc0, 0xc0, 0xfe, 0xff, 0x3, 0x3,
    0xc3, 0xff, 0x7e,

    /* U+0036 "6" */
    0x7d, 0xff, 0x1e, 0xf, 0xdf, 0xf1, 0xe3, 0xc7,
    0xfd, 0xf0,

    /* U+0037 "7" */
    0xff, 0xff, 0xb0, 0xc0, 0xc0, 0x60, 0x60, 0x30,
    0x30, 0x18, 0x18, 0xc, 0x0,

    /* U+0038 "8" */
    0x7e, 0xff, 0xc3, 0xc3, 0x7e, 0x7e, 0xc3, 0xc3,
    0xc3, 0xff, 0x7e,

    /* U+0039 "9" */
    0x7d, 0xff, 0x1e, 0x3c, 0x7f, 0xdf, 0x83, 0xc7,
    0xfd, 0xf0,

    /* U+003A ":" */
    0xff, 0x81, 0xff,

    /* U+003B ";" */
    0xff, 0x81, 0xff, 0xda, 0x0,

    /* U+003C "<" */
    0x1, 0x87, 0xdf, 0x9e, 0xf, 0x3, 0xf0, 0x3e,
    0x3,

    /* U+003D "=" */
    0xff, 0xff, 0x0, 0x0, 0xff, 0xff,

    /* U+003E ">" */
    0xc0, 0x7c, 0x1f, 0x81, 0xf0, 0xfb, 0xf3, 0xc1,
    0x80,

    /* U+003F "?" */
    0x7d, 0xff, 0x1e, 0x31, 0xe7, 0x88, 0x0, 0x0,
    0xe1, 0xc3, 0x80,

    /* U+0040 "@" */
    0x7f, 0xfb, 0xff, 0xfc, 0x0, 0xf1, 0xdb, 0xcf,
    0xef, 0x33, 0x3d, 0xcc, 0xf7, 0x33, 0xdc, 0xcf,
    0x7f, 0xfc, 0xef, 0xb0, 0x0, 0xff, 0xf1, 0xff,
    0xc0,

    /* U+0041 "A" */
    0xe, 0x1, 0xc0, 0x78, 0xd, 0x81, 0xb0, 0x77,
    0xc, 0x63, 0xfc, 0x7f, 0xcc, 0x1b, 0x83, 0x0,

    /* U+0042 "B" */
    0xfe, 0xff, 0xc3, 0xc3, 0xfe, 0xfe, 0xc3, 0xc3,
    0xc3, 0xff, 0xfe,

    /* U+0043 "C" */
    0x7e, 0xff, 0xc3, 0xc0, 0xc0, 0xc0, 0xc0, 0xc0,
    0xc3, 0xff, 0x7e,

    /* U+0044 "D" */
    0xff, 0x7f, 0xf0, 0x78, 0x3c, 0x1e, 0xf, 0x7,
    0x83, 0xc1, 0xff, 0xff, 0xc0,

    /* U+0045 "E" */
    0xff, 0xff, 0x6, 0xf, 0xdf, 0xb0, 0x60, 0xc1,
    0xff, 0xf8,

    /* U+0046 "F" */
    0xff, 0xff, 0x6, 0xf, 0xdf, 0xb0, 0x60, 0xc1,
    0x83, 0x0,

    /* U+0047 "G" */
    0x7e, 0xff, 0xc3, 0xc0, 0xc0, 0xcf, 0xcf, 0xc3,
    0xc3, 0xff, 0x7e,

    /* U+0048 "H" */
    0xc1, 0xe0, 0xf0, 0x78, 0x3f, 0xff, 0xff, 0x7,
    0x83, 0xc1, 0xe0, 0xf0, 0x60,

    /* U+0049 "I" */
    0xff, 0xff, 0xfc,

    /* U+004A "J" */
    0x6, 0xc, 0x18, 0x30, 0x60, 0xc1, 0xe3, 0xc7,
    0xfd, 0xf0,

    /* U+004B "K" */
    0xc3, 0x63, 0xb3, 0x9b, 0x8f, 0x87, 0xc3, 0xf1,
    0x98, 0xc6, 0x63, 0xb0, 0xe0,

    /* U+004C "L" */
    0xc3, 0xc, 0x30, 0xc3, 0xc, 0x30, 0xc3, 0xff,
    0xc0,

    /* U+004D "M" */
    0xe0, 0x7e, 0x7, 0xf0, 0xff, 0xf, 0xf9, 0xfd,
    0x9b, 0xd9, 0xbd, 0xfb, 0xcf, 0x3c, 0xf3, 0xc6,
    0x30,

    /* U+004E "N" */
    0xc1, 0xf0, 0xfc, 0x7e, 0x3f, 0x9e, 0xef, 0x3f,
    0x8f, 0xc7, 0xe1, 0xf0, 0x60,

    /* U+004F "O" */
    0x7f, 0x7f, 0xf0, 0x78, 0x3c, 0x1e, 0xf, 0x7,
    0x83, 0xc1, 0xff, 0xdf, 0xc0,

    /* U+0050 "P" */
    0xfe, 0xff, 0xc3, 0xc3, 0xc3, 0xff, 0xfe, 0xc0,
    0xc0, 0xc0, 0xc0,

    /* U+0051 "Q" */
    0x7f, 0x7f, 0xf0, 0x78, 0x3c, 0x1e, 0xf, 0x7,
    0x83, 0xc1, 0xff, 0xdf, 0xc0, 0xe0, 0x30, 0x1c,

    /* U+0052 "R" */
    0xfe, 0x7f, 0xb0, 0xd8, 0x6c, 0x37, 0xfb, 0xf9,
    0x9c, 0xc6, 0x63, 0xb0, 0xc0,

    /* U+0053 "S" */
    0x7e, 0xff, 0xc3, 0xc0, 0xfe, 0x7f, 0x3, 0x3,
    0xc3, 0xff, 0x7e,

    /* U+0054 "T" */
    0xff, 0xff, 0x18, 0x18, 0x18, 0x18, 0x18, 0x18,
    0x18, 0x18, 0x18,

    /* U+0055 "U" */
    0xc1, 0xe0, 0xf0, 0x78, 0x3c, 0x1e, 0xf, 0x7,
    0x83, 0xc1, 0xff, 0xdf, 0xc0,

    /* U+0056 "V" */
    0xe0, 0xec, 0x19, 0x83, 0x18, 0xc3, 0x18, 0x67,
    0x6, 0xc0, 0xd8, 0x1f, 0x1, 0xc0, 0x38, 0x0,

    /* U+0057 "W" */
    0x63, 0x86, 0xc7, 0x1d, 0x8e, 0x33, 0x1e, 0x63,
    0x6c, 0xc6, 0xdb, 0x8d, 0xbe, 0x1b, 0x3c, 0x1c,
    0x78, 0x38, 0xf0, 0x70, 0xc0,

    /* U+0058 "X" */
    0x61, 0xcc, 0x63, 0xb0, 0x7c, 0x1e, 0x3, 0x81,
    0xe0, 0x7c, 0x3b, 0x9c, 0x66, 0x1c,

    /* U+0059 "Y" */
    0x61, 0x9c, 0xe3, 0x30, 0xfc, 0x1e, 0x3, 0x0,
    0xc0, 0x30, 0xc, 0x3, 0x0, 0xc0,

    /* U+005A "Z" */
    0xff, 0xff, 0x6, 0xe, 0x1c, 0x18, 0x30, 0x70,
    0x60, 0xff, 0xff,

    /* U+005B "[" */
    0xff, 0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xcf, 0xf0,

    /* U+005C "\\" */
    0x61, 0x86, 0x1c, 0x30, 0xc3, 0x86, 0x18, 0x60,
    0xc3,

    /* U+005D "]" */
    0xff, 0x33, 0x33, 0x33, 0x33, 0x33, 0x3f, 0xf0,

    /* U+005E "^" */
    0x18, 0x3c, 0x7c, 0x66, 0xc7,

    /* U+005F "_" */
    0xff, 0xff, 0xf0,

    /* U+0060 "`" */
    0xe6,

    /* U+0061 "a" */
    0x7c, 0xfc, 0x1b, 0xff, 0xf8, 0xff, 0xbf,

    /* U+0062 "b" */
    0xc1, 0x83, 0x6, 0xf, 0xdf, 0xf1, 0xe3, 0xc7,
    0x8f, 0xff, 0xe0,

    /* U+0063 "c" */
    0x7f, 0xfc, 0x30, 0xc3, 0xf, 0xdf,

    /* U+0064 "d" */
    0x6, 0xc, 0x18, 0x37, 0xff, 0xf1, 0xe3, 0xc7,
    0x8f, 0xfb, 0xf0,

    /* U+0065 "e" */
    0x7d, 0xff, 0x1f, 0xff, 0xf8, 0x3f, 0xbf,

    /* U+0066 "f" */
    0x19, 0xcc, 0x6f, 0xfc, 0xc6, 0x31, 0x8c, 0x60,

    /* U+0067 "g" */
    0x7f, 0xfe, 0xc6, 0xfe, 0x7c, 0xc0, 0xfe, 0x7f,
    0x43, 0xff, 0x7e,

    /* U+0068 "h" */
    0xc1, 0x83, 0x6, 0xf, 0xdf, 0xf1, 0xe3, 0xc7,
    0x8f, 0x1e, 0x30,

    /* U+0069 "i" */
    0xf0, 0xff, 0xff,

    /* U+006A "j" */
    0x33, 0x0, 0x33, 0x33, 0x33, 0x33, 0x3f, 0xe0,

    /* U+006B "k" */
    0xc0, 0xc0, 0xc0, 0xc0, 0xce, 0xdc, 0xd8, 0xf0,
    0xf8, 0xfc, 0xcc, 0xc6,

    /* U+006C "l" */
    0xcc, 0xcc, 0xcc, 0xcc, 0xcc, 0xf7,

    /* U+006D "m" */
    0xfd, 0xef, 0xff, 0xc6, 0x3c, 0x63, 0xc6, 0x3c,
    0x63, 0xc6, 0x3c, 0x63,

    /* U+006E "n" */
    0xfd, 0xff, 0x1e, 0x3c, 0x78, 0xf1, 0xe3,

    /* U+006F "o" */
    0x7d, 0xff, 0x1e, 0x3c, 0x78, 0xff, 0xbe,

    /* U+0070 "p" */
    0xfd, 0xff, 0x1e, 0x3c, 0x78, 0xff, 0xfe, 0xc1,
    0x83, 0x0,

    /* U+0071 "q" */
    0x7f, 0xff, 0x1e, 0x3c, 0x78, 0xff, 0xbf, 0x6,
    0xc, 0x18,

    /* U+0072 "r" */
    0xff, 0xf1, 0x8c, 0x63, 0x18,

    /* U+0073 "s" */
    0x7f, 0xfc, 0x3e, 0x7c, 0x3f, 0xfe,

    /* U+0074 "t" */
    0x1, 0x8d, 0xff, 0x98, 0xc6, 0x31, 0xc6,

    /* U+0075 "u" */
    0xc7, 0x8f, 0x1e, 0x3c, 0x78, 0xff, 0xbf,

    /* U+0076 "v" */
    0xe3, 0x63, 0x66, 0x36, 0x36, 0x3c, 0x1c, 0x1c,

    /* U+0077 "w" */
    0x66, 0x36, 0x77, 0x6f, 0x66, 0xf6, 0x3d, 0x63,
    0x9e, 0x39, 0xc3, 0x9c,

    /* U+0078 "x" */
    0x67, 0x76, 0x3c, 0x1c, 0x1c, 0x3e, 0x76, 0x63,

    /* U+0079 "y" */
    0xe3, 0x63, 0x76, 0x36, 0x3e, 0x1c, 0x1c, 0x18,
    0x18, 0x38, 0x30,

    /* U+007A "z" */
    0xff, 0xf1, 0x8e, 0x71, 0x8f, 0xff,

    /* U+007B "{" */
    0x1c, 0xf3, 0xc, 0x30, 0xc3, 0x3c, 0xf0, 0xc3,
    0xc, 0x30, 0xf1, 0xc0,

    /* U+007C "|" */
    0xff, 0xff, 0xff, 0xfc,

    /* U+007D "}" */
    0xe3, 0xc3, 0xc, 0x30, 0xc3, 0xf, 0x3c, 0xc3,
    0xc, 0x33, 0xce, 0x0,

    /* U+007E "~" */
    0x79, 0xff, 0xf3, 0xc0
};


/*---------------------
 *  GLYPH DESCRIPTION
 *--------------------*/

static const lv_font_fmt_txt_glyph_dsc_t glyph_dsc[] = {
    {.bitmap_index = 0, .adv_w = 0, .box_w = 0, .box_h = 0, .ofs_x = 0, .ofs_y = 0} /* id = 0 reserved */,
    {.bitmap_index = 0, .adv_w = 67, .box_w = 1, .box_h = 1, .ofs_x = 0, .ofs_y = 0},
    {.bitmap_index = 1, .adv_w = 97, .box_w = 3, .box_h = 12, .ofs_x = 2, .ofs_y = 0},
    {.bitmap_index = 6, .adv_w = 128, .box_w = 6, .box_h = 5, .ofs_x = 1, .ofs_y = 7},
    {.bitmap_index = 10, .adv_w = 196, .box_w = 10, .box_h = 12, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 25, .adv_w = 168, .box_w = 8, .box_h = 16, .ofs_x = 1, .ofs_y = -2},
    {.bitmap_index = 41, .adv_w = 256, .box_w = 14, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 61, .adv_w = 170, .box_w = 9, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 74, .adv_w = 72, .box_w = 2, .box_h = 5, .ofs_x = 1, .ofs_y = 7},
    {.bitmap_index = 76, .adv_w = 98, .box_w = 4, .box_h = 15, .ofs_x = 1, .ofs_y = -3},
    {.bitmap_index = 84, .adv_w = 98, .box_w = 4, .box_h = 15, .ofs_x = 1, .ofs_y = -3},
    {.bitmap_index = 92, .adv_w = 166, .box_w = 8, .box_h = 8, .ofs_x = 1, .ofs_y = 3},
    {.bitmap_index = 100, .adv_w = 156, .box_w = 8, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 108, .adv_w = 97, .box_w = 3, .box_h = 6, .ofs_x = 2, .ofs_y = -3},
    {.bitmap_index = 111, .adv_w = 115, .box_w = 5, .box_h = 2, .ofs_x = 1, .ofs_y = 3},
    {.bitmap_index = 113, .adv_w = 97, .box_w = 3, .box_h = 3, .ofs_x = 2, .ofs_y = 0},
    {.bitmap_index = 115, .adv_w = 106, .box_w = 6, .box_h = 12, .ofs_x = 0, .ofs_y = 0},
    {.bitmap_index = 124, .adv_w = 173, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 135, .adv_w = 148, .box_w = 7, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 145, .adv_w = 164, .box_w = 7, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 155, .adv_w = 169, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 166, .adv_w = 163, .box_w = 9, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 179, .adv_w = 168, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 190, .adv_w = 165, .box_w = 7, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 200, .adv_w = 160, .box_w = 9, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 213, .adv_w = 168, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 224, .adv_w = 165, .box_w = 7, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 234, .adv_w = 97, .box_w = 3, .box_h = 8, .ofs_x = 2, .ofs_y = 0},
    {.bitmap_index = 237, .adv_w = 97, .box_w = 3, .box_h = 11, .ofs_x = 2, .ofs_y = -3},
    {.bitmap_index = 242, .adv_w = 169, .box_w = 9, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 251, .adv_w = 177, .box_w = 8, .box_h = 6, .ofs_x = 1, .ofs_y = 1},
    {.bitmap_index = 257, .adv_w = 169, .box_w = 9, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 266, .adv_w = 151, .box_w = 7, .box_h = 12, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 277, .adv_w = 261, .box_w = 14, .box_h = 14, .ofs_x = 1, .ofs_y = -3},
    {.bitmap_index = 302, .adv_w = 173, .box_w = 11, .box_h = 11, .ofs_x = 0, .ofs_y = 0},
    {.bitmap_index = 318, .adv_w = 170, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 329, .adv_w = 173, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 340, .adv_w = 183, .box_w = 9, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 353, .adv_w = 141, .box_w = 7, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 363, .adv_w = 138, .box_w = 7, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 373, .adv_w = 170, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 384, .adv_w = 186, .box_w = 9, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 397, .adv_w = 79, .box_w = 2, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 400, .adv_w = 140, .box_w = 7, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 410, .adv_w = 177, .box_w = 9, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 423, .adv_w = 125, .box_w = 6, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 432, .adv_w = 229, .box_w = 12, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 449, .adv_w = 191, .box_w = 9, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 462, .adv_w = 183, .box_w = 9, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 475, .adv_w = 160, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 486, .adv_w = 183, .box_w = 9, .box_h = 14, .ofs_x = 1, .ofs_y = -3},
    {.bitmap_index = 502, .adv_w = 170, .box_w = 9, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 515, .adv_w = 168, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 526, .adv_w = 145, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 537, .adv_w = 183, .box_w = 9, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 550, .adv_w = 175, .box_w = 11, .box_h = 11, .ofs_x = 0, .ofs_y = 0},
    {.bitmap_index = 566, .adv_w = 246, .box_w = 15, .box_h = 11, .ofs_x = 0, .ofs_y = 0},
    {.bitmap_index = 587, .adv_w = 169, .box_w = 10, .box_h = 11, .ofs_x = 0, .ofs_y = 0},
    {.bitmap_index = 601, .adv_w = 159, .box_w = 10, .box_h = 11, .ofs_x = 0, .ofs_y = 0},
    {.bitmap_index = 615, .adv_w = 152, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 626, .adv_w = 95, .box_w = 4, .box_h = 15, .ofs_x = 1, .ofs_y = -3},
    {.bitmap_index = 634, .adv_w = 106, .box_w = 6, .box_h = 12, .ofs_x = 0, .ofs_y = 0},
    {.bitmap_index = 643, .adv_w = 95, .box_w = 4, .box_h = 15, .ofs_x = 0, .ofs_y = -3},
    {.bitmap_index = 651, .adv_w = 157, .box_w = 8, .box_h = 5, .ofs_x = 1, .ofs_y = 6},
    {.bitmap_index = 656, .adv_w = 154, .box_w = 10, .box_h = 2, .ofs_x = 0, .ofs_y = -3},
    {.bitmap_index = 659, .adv_w = 128, .box_w = 4, .box_h = 2, .ofs_x = 3, .ofs_y = 9},
    {.bitmap_index = 660, .adv_w = 140, .box_w = 7, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 667, .adv_w = 155, .box_w = 7, .box_h = 12, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 678, .adv_w = 132, .box_w = 6, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 684, .adv_w = 155, .box_w = 7, .box_h = 12, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 695, .adv_w = 145, .box_w = 7, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 702, .adv_w = 105, .box_w = 5, .box_h = 12, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 710, .adv_w = 150, .box_w = 8, .box_h = 11, .ofs_x = 1, .ofs_y = -3},
    {.bitmap_index = 721, .adv_w = 155, .box_w = 7, .box_h = 12, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 732, .adv_w = 73, .box_w = 2, .box_h = 12, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 735, .adv_w = 73, .box_w = 4, .box_h = 15, .ofs_x = -1, .ofs_y = -3},
    {.bitmap_index = 743, .adv_w = 141, .box_w = 8, .box_h = 12, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 755, .adv_w = 84, .box_w = 4, .box_h = 12, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 761, .adv_w = 218, .box_w = 12, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 773, .adv_w = 152, .box_w = 7, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 780, .adv_w = 152, .box_w = 7, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 787, .adv_w = 155, .box_w = 7, .box_h = 11, .ofs_x = 1, .ofs_y = -3},
    {.bitmap_index = 797, .adv_w = 155, .box_w = 7, .box_h = 11, .ofs_x = 1, .ofs_y = -3},
    {.bitmap_index = 807, .adv_w = 105, .box_w = 5, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 812, .adv_w = 140, .box_w = 6, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 818, .adv_w = 110, .box_w = 5, .box_h = 11, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 825, .adv_w = 152, .box_w = 7, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 832, .adv_w = 137, .box_w = 8, .box_h = 8, .ofs_x = 0, .ofs_y = 0},
    {.bitmap_index = 840, .adv_w = 197, .box_w = 12, .box_h = 8, .ofs_x = 0, .ofs_y = 0},
    {.bitmap_index = 852, .adv_w = 137, .box_w = 8, .box_h = 8, .ofs_x = 0, .ofs_y = 0},
    {.bitmap_index = 860, .adv_w = 140, .box_w = 8, .box_h = 11, .ofs_x = 0, .ofs_y = -3},
    {.bitmap_index = 871, .adv_w = 122, .box_w = 6, .box_h = 8, .ofs_x = 1, .ofs_y = 0},
    {.bitmap_index = 877, .adv_w = 104, .box_w = 6, .box_h = 15, .ofs_x = 0, .ofs_y = -3},
    {.bitmap_index = 889, .adv_w = 92, .box_w = 2, .box_h = 15, .ofs_x = 2, .ofs_y = -3},
    {.bitmap_index = 893, .adv_w = 104, .box_w = 6, .box_h = 15, .ofs_x = 0, .ofs_y = -3},
    {.bitmap_index = 905, .adv_w = 172, .box_w = 9, .box_h = 3, .ofs_x = 1, .ofs_y = 3}
};

/*---------------------
 *  CHARACTER MAPPING
 *--------------------*/



/*Collect the unicode lists and glyph_id offsets*/
static const lv_font_fmt_txt_cmap_t cmaps[] =
{
    {
        .range_start = 32, .range_length = 95, .glyph_id_start = 1,
        .unicode_list = NULL, .glyph_id_ofs_list = NULL, .list_length = 0, .type = LV_FONT_FMT_TXT_CMAP_FORMAT0_TINY
    }
};

/*-----------------
 *    KERNING
 *----------------*/


/*Pair left and right glyphs for kerning*/
static const uint8_t kern_pair_glyph_ids[] =
{
    34, 53,
    34, 55,
    34, 56,
    34, 58,
    39, 43,
    45, 53,
    45, 55,
    45, 56,
    45, 58,
    53, 34,
    53, 43,
    55, 34,
    56, 34,
    58, 34,
    58, 43
};

/* Kerning between the respective left and right glyphs
 * 4.4 format which needs to scaled with `kern_scale`*/
static const int8_t kern_pair_values[] =
{
    -13, -13, -8, -15, -20, -15, -10, -5,
    -10, -13, -15, -13, -8, -15, -20
};

/*Collect the kern pair's data in one place*/
static const lv_font_fmt_txt_kern_pair_t kern_pairs =
{
    .glyph_ids = kern_pair_glyph_ids,
    .values = kern_pair_values,
    .pair_cnt = 15,
    .glyph_ids_size = 0
};

/*--------------------
 *  ALL CUSTOM DATA
 *--------------------*/

#if LVGL_VERSION_MAJOR == 8
/*Store all the custom data of the font*/
static  lv_font_fmt_txt_glyph_cache_t cache;
#endif

#if LVGL_VERSION_MAJOR >= 8
static const lv_font_fmt_txt_dsc_t font_dsc = {
#else
static lv_font_fmt_txt_dsc_t font_dsc = {
#endif
    .glyph_bitmap = glyph_bitmap,
    .glyph_dsc = glyph_dsc,
    .cmaps = cmaps,
    .kern_dsc = &kern_pairs,
    .kern_scale = 16,
    .cmap_num = 1,
    .bpp = 1,
    .kern_classes = 0,
    .bitmap_format = 0,
#if LVGL_VERSION_MAJOR == 8
    .cache = &cache
#endif
};



/*-----------------
 *  PUBLIC FONT
 *----------------*/

/*Initialize a public general font descriptor*/
#if LVGL_VERSION_MAJOR >= 8
const lv_font_t ui_font_Quantico_16 = {
#else
lv_font_t ui_font_Quantico_16 = {
#endif
    .get_glyph_dsc = lv_font_get_glyph_dsc_fmt_txt,    /*Function pointer to get glyph's data*/
    .get_glyph_bitmap = lv_font_get_bitmap_fmt_txt,    /*Function pointer to get glyph's bitmap*/
    .line_height = 17,          /*The maximum line height required by the font*/
    .base_line = 3,             /*Baseline measured from the bottom of the line*/
#if !(LVGL_VERSION_MAJOR == 6 && LVGL_VERSION_MINOR == 0)
    .subpx = LV_FONT_SUBPX_NONE,
#endif
#if LV_VERSION_CHECK(7, 4, 0) || LVGL_VERSION_MAJOR >= 8
    .underline_position = -1,
    .underline_thickness = 1,
#endif
    .dsc = &font_dsc,          /*The custom font data. Will be accessed by `get_glyph_bitmap/dsc` */
#if LV_VERSION_CHECK(8, 2, 0) || LVGL_VERSION_MAJOR >= 9
    .fallback = NULL,
#endif
    .user_data = NULL,
};



#endif /*#if UI_FONT_QUANTICO_16*/

