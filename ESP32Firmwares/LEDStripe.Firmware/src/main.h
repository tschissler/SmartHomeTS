#pragma once

enum Panel {
  LEFT,
  RIGHT,
  BOTH
};

void setLEDColor(int r, int g, int b, int d, Panel panel);
