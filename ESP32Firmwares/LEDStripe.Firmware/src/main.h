#pragma once

enum Panel {
  LEFT,
  RIGHT,
  BOTH
};

void setLEDColor(int d, int r, int g, int b, Panel panel);