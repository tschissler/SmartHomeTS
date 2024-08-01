#pragma once

void setLEDColor(int d, int r, int g, int b, Panel panel);

enum Panel {
  LEFT,
  RIGHT,
  BOTH
};