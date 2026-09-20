#include <Arduino.h>
#include <ArduinoJson.h>
#include <FastLED.h>
#include <WiFi.h>
#include <time.h>
#include "main.h"

// Shared libraries (same structure as TemperatureSensor2)
#include "ESP32Helpers.h"
#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"

const char* version = FIRMWARE_VERSION;

#define DATA_PIN_1 13
#define DATA_PIN_2 12
#define NUM_LEDS 256
CRGB ledsRight[NUM_LEDS];
CRGB ledsLeft[NUM_LEDS];

// Der Streifen bekommt die Farbe so, wie ein Bildschirm sie bekommt: gamma-kodiert.
// 2,2 ist der Exponent der sRGB-Kurve, und genau in sRGB rechnet der Browser, der die
// Vorschau in der Web-App zeichnet -- derselbe Zahlenwert ergibt damit hier dieselbe
// wahrgenommene Helligkeit wie dort. Die PWM des WS2812B ist dagegen linear; ohne diese
// Korrektur leuchtet jeder mittlere Wert deutlich zu hell.
//
// Ein groesserer Exponent macht es unten nicht feiner, sondern groeber: Nachgerechnet
// fallen bei 2,2 die Helligkeitsstufen 1..11 der Web-App auf denselben PWM-Wert 1, bei
// 2,6 schon die Stufen 1..15 und bei 2,8 die Stufen 1..17. Mehr Aufloesung im Dunkeln
// gaebe nur mehr als 8 Bit PWM.
static const float GAMMA = 2.2f;

// Weissabgleich: Der gruene Kanal eines WS2812B ist deutlich effizienter als Rot und
// Blau. Ohne Korrektur hat jedes Weiss und jedes Pastell einen Gruenstich. Die Faktoren
// sind die von FastLEDs TypicalLEDStrip (0xFFB0F0).
static const uint8_t WEISSABGLEICH_R = 255;
static const uint8_t WEISSABGLEICH_G = 176;
static const uint8_t WEISSABGLEICH_B = 240;

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern:
// WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);
WiFiClient wifiClient;

static MQTTClientLib *mqttClientLib = nullptr;
static String chipID = "";

static bool otaActive = false;
static unsigned long otaStartMs = 0;
static bool otaEnable = String(OTA_ENABLED) != "false";

// MQTT settings (kept compatible with existing backend topics)
static const String mqtt_broker = "mosquitto.intern";
static const int mqtt_port = 1883;
static const String mqtt_update_topic = "OTAUpdate/LEDStripe";
// Retained device heartbeat on status/<location>/<deviceType>/<deviceName>, so a consumer
// can tell a silent device from a healthy one. Must match the OTA topic segment.
static const uint32_t HEARTBEAT_INTERVAL_MS = 60000;
static uint32_t lastHeartbeatMs = 0;
static const String mqtt_data_topic = "commands/illumination/LEDStripe/setColor";

static void mqttCallback(String &topic, String &payload);
static void connectToMQTT(bool cleanSession);

void setupTime() {
  // Set timezone (e.g., UTC +1:00)
  // Change according to your timezone
  // For UTC -5:00, use -5 * 3600, and so on
  configTime(1 * 3600, 0, "pool.ntp.org", "time.nist.gov");

  // Wait until time is set 
  const unsigned long startMs = millis();
  time_t now = time(nullptr);
  while (now < 8 * 3600 * 2) {
    if (millis() - startMs > 15000) {
      Serial.println("Timed out waiting for NTP time; continuing without valid time");
      break;
    }
    delay(250);
    now = time(nullptr);
  }

  struct tm timeinfo;
  if (!getLocalTime(&timeinfo)) {
    Serial.println("Failed to obtain time");
    return;
  }
  Serial.println(&timeinfo, "%A, %B %d %Y %H:%M:%S");
}

void clear(Panel panel) {
  for (int i = 0; i < NUM_LEDS; i++) {
    if (panel == Panel::BOTH || panel == Panel::RIGHT) {
      ledsRight[i] = CRGB(0, 0, 0);
    }
    if (panel == Panel::BOTH || panel == Panel::LEFT) {
      ledsLeft[i] = CRGB(0, 0, 0);
    }
  }
  FastLED.show();
}

void setColorFromJson(String jsonPayload) {
  // Parse JSON
  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, jsonPayload);
  if (error) {
    Serial.print("deserializeJson() failed: ");
    Serial.println(error.c_str());
    return;
  }

  // Extract RGBD values for left and right panels
  int rLeft = doc["Left"]["Red"];
  int gLeft = doc["Left"]["Green"];
  int bLeft = doc["Left"]["Blue"];
  int dLeft = doc["Left"]["Density"];
  bool onLeft = doc["Left"]["On"];

  int rRight = doc["Right"]["Red"];
  int gRight = doc["Right"]["Green"];
  int bRight = doc["Right"]["Blue"];
  int dRight = doc["Right"]["Density"];
  bool onRight = doc["Right"]["On"];

  if (onLeft) {
    setLEDColor(rLeft, gLeft, bLeft, dLeft, LEFT);
  } else {
    clear(LEFT);
  }
  if (onRight) {
    setLEDColor(rRight, gRight, bRight, dRight, RIGHT);
  } else {
    clear(RIGHT);
  }
}

void setLEDColor(int r, int g, int b, int d, Panel panel) {
  // Voller Fuellgrad heisst "alle an" und laeuft deshalb nicht ueber die Modulo-Formel.
  // Ueber sie gerechnet blieb ausgerechnet LED 0 dunkel: Der Sonderfall setzte
  // trigger2 = 999, und 0 % 999 == 0 macht die Bedingung (i % trigger2 != 0) falsch --
  // 255 von 256 LEDs. Ein Wert ueber 100, wie ihn die Web-App frueher als Maximum
  // schickte, war schlimmer statt besser: 101 ergibt trigger2 = 100 / -1 = -100, und
  // weil das Vorzeichen in C++ dem Dividenden folgt, blieben die LEDs 0, 100 und 200
  // dunkel -- 253 von 256.
  const bool alleAn = (d >= 100);
  int trigger = (d == 0 ? 999 : 100 / d);
  // Bei alleAn wird trigger2 nicht mehr gelesen; die 1 steht nur, damit an dieser
  // Stelle nicht durch 0 geteilt wird.
  int trigger2 = (alleAn ? 1 : 100 / (100 - d));

  // Alle LEDs eines Panels bekommen dieselbe Farbe, also werden Gamma und Weissabgleich
  // einmal je Nachricht gerechnet und nicht 256-mal. Deshalb braucht es auch keine
  // Wertetabelle im Flash.
  CRGB farbe = CRGB(r, g, b);

  // Die _video-Variante zieht einen Wert > 0 nie auf 0 -- eine dunkle Farbe bleibt sonst
  // nicht dunkel, sondern verschwindet.
  napplyGamma_video(farbe, GAMMA);

  // Der Weissabgleich wird hier von Hand gerechnet und nicht ueber .setCorrection() am
  // Controller. Der Grund ist der Zusammenstoss der beiden Korrekturen im untersten
  // Bereich: FastLED skaliert beim show() mit scale8(), und scale8(1, 176) ist 0. Die 1,
  // auf die napplyGamma_video gerade geklemmt hat, faellt damit wieder weg -- Gruen und
  // Blau verschwinden bei wenig Helligkeit ganz, und aus einem Grau wird Rot. Genau die
  // Farbverschiebung also, die diese Korrektur beseitigen soll. scale8_video haelt einen
  // Wert > 0 bei mindestens 1 und hat das Problem nicht.
  farbe.r = scale8_video(farbe.r, WEISSABGLEICH_R);
  farbe.g = scale8_video(farbe.g, WEISSABGLEICH_G);
  farbe.b = scale8_video(farbe.b, WEISSABGLEICH_B);

  // Helper function to set LED color
  auto setLed = [&](int i, bool condition) {
    if (panel == Panel::BOTH || panel == Panel::RIGHT) {
      ledsRight[i] = condition ? farbe : CRGB(0, 0, 0);
    } 
    if (panel == Panel::BOTH || panel == Panel::LEFT) {
      ledsLeft[i] = condition ? farbe : CRGB(0, 0, 0);
    }
  };

  // Set LED color
  for (int i = 0; i < NUM_LEDS; i++) {
    bool condition = alleAn || ((d <= 50) ? (i % trigger == 0) : (i % trigger2 != 0));
    setLed(i, condition);
  }

  FastLED.show();
}

// Die Startanzeige laeuft bewusst ohne Gamma und ohne Weissabgleich: Sie ist keine Farbe,
// die stimmen muss, sondern das einzige Zeichen am Geraet selbst, dass es nach einem
// OTA-Update wieder hochgekommen ist. Korrigiert waere aus (10, 80, 10) ein kaum
// sichtbares Glimmen geworden.
void initLEDGreen(Panel panel) {
  for (int i = 0; i < NUM_LEDS; i++) {
    if (panel == Panel::BOTH || panel == Panel::RIGHT) {
      ledsRight[i] = CRGB(10, 80, 10);
    }
    if (panel == Panel::BOTH || panel == Panel::LEFT) {
      ledsLeft[i] = CRGB(10, 80, 10);
    }
  }
  FastLED.show();
}

static void mqttCallback(String &topic, String &payload)
{
  Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

  if (topic == mqtt_update_topic)
  {
    if (otaActive || !otaEnable)
    {
      if (otaActive)
        Serial.println("OTA in progress, ignoring message");
      if (!otaEnable)
        Serial.println("OTA disabled, ignoring message");
      return;
    }

    String updateVersion = AzureOTAUpdater::ExtractVersionFromUrl(payload);
    Serial.println("Current firmware version is " + String(version));
    Serial.println("New firmware version is " + updateVersion);

    if (updateVersion.length() > 0 && String(version) == updateVersion)
    {
      Serial.println("Firmware is up to date");
      return;
    }

    Serial.println("OTA Update Triggered");
    otaActive = true;
    otaStartMs = millis();
    AzureOTAUpdater::UpdateFirmwareFromUrl(payload.c_str());
    return;
  }

  if (topic == mqtt_data_topic)
  {
    Serial.println("Setting LED color from MQTT message");
    setColorFromJson(payload);
    return;
  }

  Serial.println("Unknown topic, ignoring message");
}

static void connectToMQTT(bool cleanSession)
{
  Serial.print("WiFi Status: ");
  Serial.println(WiFi.status() == WL_CONNECTED ? "Connected" : "Disconnected");

  if (WiFi.status() != WL_CONNECTED)
  {
    Serial.println("WiFi not connected, attempting to reconnect...");
    wifiLib.connect();
  }

  mqttClientLib->connect(cleanSession);
  mqttClientLib->subscribe({mqtt_update_topic, mqtt_data_topic});
  Serial.println("### MQTT Client is connected and subscribed to topics");
  Serial.println("OTA Topic: " + mqtt_update_topic);
  Serial.println("Data Topic: " + mqtt_data_topic);
}

void setup() {
  FastLED.addLeds<WS2812B, DATA_PIN_1, GRB>(ledsRight, NUM_LEDS);
  FastLED.addLeds<WS2812B, DATA_PIN_2, GRB>(ledsLeft, NUM_LEDS);
  Serial.begin(115200);
  Serial.print("LEDStripe ");
  Serial.println(version);

  chipID = ESP32Helpers::getChipId();
  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);

  // Connect to WiFi (same structure as TemperatureSensor2)
  Serial.print("Connecting to WiFi ");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();

  // Disable WiFi modem sleep: power-save causes latency spikes/packet loss that
  // break long TLS transfers (OTA download aborts with ECONNRESET / errno 104).
  WiFi.setSleep(false);

  setupTime();

  initLEDGreen(LEFT);
  delay(500);
  clear(LEFT);
  initLEDGreen(RIGHT);
  delay(500);
  clear(RIGHT);
  initLEDGreen(LEFT);
  delay(500);
  clear(LEFT);
  initLEDGreen(RIGHT);
  delay(500);
  clear(RIGHT);
  
  // Set up MQTT
  String mqttClientID = "ESP32LEDStripeClient_" + chipID;
  mqttClientLib = new MQTTClientLib(mqtt_broker, mqtt_port, mqttClientID, wifiClient, mqttCallback);
  connectToMQTT(true);

  mqttClientLib->publish("meta/LEDStripe/version", String(version), true, 2);
}

void loop() {
  if (otaActive)
  {
    const int otaStatus = AzureOTAUpdater::CheckUpdateStatus();
    if (otaStatus < 0)
    {
      Serial.println("OTA failed; resuming normal operation. Next OTA will trigger on new MQTT message.");
      otaActive = false;
    }
    else if (otaStatus == 0)
    {
      // If OTA didn't transition to UPDATING within a reasonable time, give up and stay functional.
      if (millis() - otaStartMs > 120000)
      {
        Serial.println("OTA did not succeed within timeout; resuming normal operation.");
        otaActive = false;
      }
    }

    delay(200);
    return;
  }

  if (WiFi.status() != WL_CONNECTED)
  {
    Serial.println("WiFi disconnected, rescanning and reconnecting...");
    wifiLib.scanAndSelectNetwork();
    wifiLib.connect();
  }

  {
    bool mqttConnected = mqttClientLib->loop();

    if (millis() - lastHeartbeatMs >= HEARTBEAT_INTERVAL_MS) {
      lastHeartbeatMs = millis();
      mqttClientLib->publishStatus("M1", "LEDStripe", "LEDStripe", String(version));
    }
    if (!mqttConnected)
    {
      int lastErr = mqttClientLib->lastError();
      Serial.print("MQTT loop() returned false! Last Error Code: ");
      Serial.println(lastErr);
      Serial.print("WiFi Status: ");
      Serial.println(WiFi.status() == WL_CONNECTED ? "Connected" : "Disconnected");
      Serial.println("MQTT Client not connected, reconnecting in loop...");
      connectToMQTT(false);
    }
  }

  delay(200);
}

