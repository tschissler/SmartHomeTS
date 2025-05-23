#include <cstdint>
#ifndef COLOR_H
#define COLOR_H

struct Color {
  uint8_t r, g, b;
};

// Common HTML/CSS-style color names mapped to 24-bit RGB
const Color ALICEBLUE       = {240, 248, 255};
const Color ANTIQUEWHITE    = {250, 235, 215};
const Color AQUA            = {0, 255, 255};
const Color AQUAMARINE      = {127, 255, 212};
const Color AZURE           = {240, 255, 255};
const Color BEIGE           = {245, 245, 220};
const Color BLACK           = {0, 0, 0};
const Color BLUE            = {0, 0, 255};
const Color BLUEVIOLET      = {138, 43, 226};
const Color BROWN           = {165, 42, 42};
const Color CHARTREUSE      = {127, 255, 0};
const Color CORAL           = {255, 127, 80};
const Color CRIMSON         = {220, 20, 60};
const Color CYAN            = {0, 255, 255};
const Color DARKBLUE        = {0, 0, 139};
const Color DARKCYAN        = {0, 139, 139};
const Color DARKGOLDENROD   = {184, 134, 11};
const Color DARKGRAY        = {169, 169, 169};
const Color DARKGREEN       = {0, 100, 0};
const Color DARKMAGENTA     = {139, 0, 139};
const Color DARKORANGE      = {255, 140, 0};
const Color DARKRED         = {139, 0, 0};
const Color DARKVIOLET      = {148, 0, 211};
const Color DEEPPINK        = {255, 20, 147};
const Color DODGERBLUE      = {30, 144, 255};
const Color GOLD            = {255, 215, 0};
const Color GRAY            = {128, 128, 128};
const Color GREEN           = {0, 255, 0};
const Color INDIGO          = {75, 0, 130};
const Color MAGENTA         = {255, 0, 255};
const Color ORANGE          = {255, 165, 0};
const Color RED             = {255, 0, 0};
const Color WHITE           = {255, 255, 255};
const Color YELLOW          = {255, 255, 0};

#endif