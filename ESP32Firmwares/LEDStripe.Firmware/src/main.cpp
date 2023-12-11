#include <Arduino.h>
#include <FastLED.h>
#include <WiFi.h>
#include <WebServer.h>

#define DATA_PIN_1 13
#define DATA_PIN_2 12
#define NUM_LEDS 256
CRGB leds[NUM_LEDS];

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_SSID and WIFI_PASSWORD as environment variables on your dev-system
const char* ssid = WIFI_SSID;
const char* password = WIFI_PASSWORD;

WebServer server(80);

void setColor() {
  // Check if each parameter exists
  if (server.hasArg("r") && server.hasArg("g") && server.hasArg("b") && server.hasArg("d")) {
    int r = server.arg("r").toInt();
    int g = server.arg("g").toInt();
    int b = server.arg("b").toInt();
    int d = server.arg("d").toInt();

    int trigger = (d==0?999:100 / d);
    int trigger2 = (d==100?999:100/(100-d));

    // Set LED color
    for (int i = 0; i < NUM_LEDS; i++) {
      if (d <= 50) {
        if (i % trigger == 0) {
          leds[i] = CRGB(r, g, b);
        } else {
          leds[i] = CRGB(0, 0, 0);
        }
      } else {
        if (i % trigger2 == 0) {
          leds[i] = CRGB(0, 0, 0);
        } else {
          leds[i] = CRGB(r, g, b);
        }
      }
    }
    FastLED.show();

    server.send(200, "text/plain", "Color set to RGB(" + String(r) + "," + String(g) + "," + String(b) + ") with density " + String(d));
  } else {
    server.send(400, "text/plain", "Invalid request");
  }
}

void clear() {
  for (int i = 0; i < NUM_LEDS; i++) {
    leds[i] = CRGB(0, 0, 0);
  }
  FastLED.show();
}

void initLED() {
  for (int i = 0; i < 16; i++) {
    leds[i] = CRGB(10, 5, 2);
  }
  FastLED.show();
}

void setup() {
  FastLED.addLeds<WS2812B, DATA_PIN_1, GRB>(leds, NUM_LEDS);
  FastLED.addLeds<WS2812B, DATA_PIN_2, GRB>(leds, NUM_LEDS);
  Serial.begin(9600);
  Serial.println("LEDStripe");
  initLED();

  clear();

  // Connect to WiFi
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(1000);
    Serial.println("Connecting to WiFi...");
  }
  Serial.println("Connected to WiFi");
  
  // Print the IP address
  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  // Define endpoint
  server.on("/setColor", HTTP_GET, setColor);

  // Start server
  server.begin();
}

void loop() {
  server.handleClient();
}
