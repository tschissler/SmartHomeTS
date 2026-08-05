#include <Arduino.h>
#include <ArduinoJson.h>
#include <map>
#include <NTPClient.h>
#include <time.h>

#include <ESP32Ping.h>
#include <esp_mac.h>
#include <OneWire.h>
#include <DallasTemperature.h>
#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"

// ---------------------------------------------------------------------------
// Hardware configuration
//
// This firmware is a deliberately "dumb" actuator/sensor node: it receives
// the mixer target position (open/close) via MQTT from the RulesEngine
// service and drives the actuators. The control logic (when to open/close)
// lives in the RulesEngine, not here.
//
// Relay board: 16-channel board with JQC-3FF-S-Z relays (12V coils).
// The GPIOs drive the board's opto inputs in open-drain mode: LOW sinks the
// input to GND (relay ON), HIGH releases the pin to high-impedance (relay
// OFF) — an actively driven 3.3V high cannot release the 5V-referenced
// inputs. See the test programs for details.
//
// Wiring per mixer: one relay acts as direction selector (Wechsler),
// one relay switches the actuator movement on/off.
// Fail-safe wiring: direction relay de-energized (NC) = direction OPEN,
// run relay de-energized = actuator stopped.
// ---------------------------------------------------------------------------

#define MIXER1_RUN_PIN 16
#define MIXER1_DIRECTION_PIN 17
#define MIXER2_RUN_PIN 18
#define MIXER2_DIRECTION_PIN 19

#define ONEWIRE_BUS1_PIN 25
#define ONEWIRE_BUS2_PIN 26
#define ONEWIRE_BUS3_PIN 27
#define ONEWIRE_BUS_COUNT 3

const char* version = FIRMWARE_VERSION;
String chipID = "";

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);

WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP, "pool.ntp.org", 3600, 60000);
std::unique_ptr<MQTTClientLib> mqttClient = nullptr;

static bool otaInProgress = false;
static bool otaEnable = true;

const String mqtt_broker = "mosquitto.intern";
const int mqtt_port = 1883;
static String mqtt_OTAtopic = "OTAUpdate/MixerController";
static String mqtt_ConfigTopic = "config/MixerController/";       // + chipID
static String mqtt_CommandsTopic = "commands/MixerController/#";

// ---------------------------------------------------------------------------
// Runtime configuration (updated via retained MQTT config message)
// ---------------------------------------------------------------------------
static String location = "M1";
static uint32_t travelTimeSeconds = 140;        // actuator full travel time
static uint32_t temperatureIntervalSeconds = 60;
static std::map<String, String> sensorNames;    // ROM address -> display name

// ---------------------------------------------------------------------------
// Mixer state machine
//
// Two kinds of moves, both without position feedback:
// - Full travels ("open"/"close"): run 115% of the configured travel time,
//   so each move is also a reference run against the actuator's end stop.
// - Pulses ("open:N"/"close:N"): run exactly N seconds towards one end,
//   used by the RulesEngine step controller to hold intermediate positions.
//   The position is then only an estimate (seconds since the last reference
//   run); a full travel restores certainty.
// ---------------------------------------------------------------------------
enum class MixerPosition { Unknown, Open, Closed, Partial };

static const char* positionToString(MixerPosition p) {
  switch (p) {
    case MixerPosition::Open: return "open";
    case MixerPosition::Closed: return "closed";
    case MixerPosition::Partial: return "partial";
    default: return "unknown";
  }
}

class MixerActuator {
 public:
  MixerActuator(const char* name, int directionPin, int runPin)
      : name(name), directionPin(directionPin), runPin(runPin) {}

  void begin() {
    // Set the output latch to inactive BEFORE switching the pin to output,
    // so the relays never see an on-glitch during startup
    relayOff(runPin);
    relayOff(directionPin);
    pinMode(runPin, OUTPUT_OPEN_DRAIN);
    pinMode(directionPin, OUTPUT_OPEN_DRAIN);
    relayOff(runPin);
    relayOff(directionPin);
  }

  // The firmware simply follows the last received command, indefinitely.
  // Commands are published retained by the RulesEngine, so the target is
  // restored right after every reconnect. Monitoring compares target vs.
  // position via the published state instead of a local watchdog.
  // target always keeps the last commanded full-travel position; a completed
  // pulse sets `holding` instead, so the actuator stays put even though
  // position (partial) and target differ.
  void setTarget(MixerPosition t) {
    target = t;
    holding = false;
  }

  // Queue a relative move; executed as soon as the actuator is idle.
  // A later full-travel command discards a still pending pulse.
  void requestPulse(bool towardsClose, uint32_t seconds) {
    pulsePending = true;
    pulseTowardsClose = towardsClose;
    pulseSeconds = seconds;
  }

  MixerPosition getTarget() const { return target; }
  MixerPosition getPosition() const { return current; }
  bool isMoving() const { return state == State::Running; }
  bool hasPositionEstimate() const { return estimateValid; }
  int getOpenPercent() const {
    if (!estimateValid || travelTimeSeconds == 0) return -1;
    float fraction = 1.0f - estimatedSeconds / (float)travelTimeSeconds;
    return (int)(fraction * 100.0f + 0.5f);
  }

  const char* name;

  // Returns true whenever the externally visible state changed (for publishing)
  bool update() {
    unsigned long now = millis();
    switch (state) {
      case State::Idle:
        if (!holding && current != target) {
          pulsePending = false;  // full travel supersedes a queued pulse
          setDirection(target == MixerPosition::Closed);
          movingTo = target;
          targetAtMoveStart = target;
          moveMs = 0;  // 0 = full travel
          state = State::PrepareDirection;
          stateMs = now;
        } else if (pulsePending) {
          pulsePending = false;
          uint32_t effective = clampPulseToRemainingTravel(pulseTowardsClose, pulseSeconds);
          if (effective == 0) {
            Serial.println(String(name) + ": pulse ignored, already at end stop");
            return false;
          }
          setDirection(pulseTowardsClose);
          movingTo = MixerPosition::Partial;
          targetAtMoveStart = target;
          holding = false;  // moving, not holding — restored when the pulse completes
          moveTowardsClose = pulseTowardsClose;
          moveMs = effective * 1000UL;
          state = State::PrepareDirection;
          stateMs = now;
        }
        return false;

      case State::PrepareDirection:
        // Give the direction relay time to settle before powering the actuator
        if (now - stateMs >= 300) {
          relayOn(runPin);
          state = State::Running;
          stateMs = now;
          Serial.println(String(name) + (moveMs == 0
              ? ": driving to " + String(positionToString(movingTo))
              : ": pulse " + String(moveMs / 1000UL) + "s towards " + (moveTowardsClose ? "close" : "open")));
          return true;
        }
        return false;

      case State::Running:
        if (target != targetAtMoveStart) {
          // A new command arrived mid-move: stop and let Idle restart cleanly
          relayOff(runPin);
          current = MixerPosition::Unknown;
          estimateValid = false;
          state = State::Cooldown;
          stateMs = now;
          return true;
        }
        if (now - stateMs >= (moveMs != 0 ? moveMs : fullTravelMs())) {
          relayOff(runPin);
          if (moveMs == 0) {
            current = movingTo;
            estimatedSeconds = (movingTo == MixerPosition::Closed) ? (float)travelTimeSeconds : 0.0f;
            estimateValid = true;
          } else {
            float deltaSeconds = (float)(moveMs / 1000UL);
            estimatedSeconds += moveTowardsClose ? deltaSeconds : -deltaSeconds;
            if (estimatedSeconds < 0.0f) estimatedSeconds = 0.0f;
            if (estimatedSeconds > (float)travelTimeSeconds) estimatedSeconds = (float)travelTimeSeconds;
            current = MixerPosition::Partial;
            // Hold here until the next command — without this the Idle state
            // would immediately drive back to the retained full-travel target
            holding = true;
          }
          state = State::Cooldown;
          stateMs = now;
          Serial.println(String(name) + ": reached " + positionToString(current) +
                         (estimateValid ? " (~" + String(getOpenPercent()) + "% open)" : ""));
          return true;
        }
        return false;

      case State::Cooldown:
        if (now - stateMs >= 500) {
          // Motor current has ceased — now the direction relay can release
          // without switching under load; no point keeping its coil energized
          relayOff(directionPin);
          state = State::Idle;
        }
        return false;
    }
    return false;
  }

 private:
  enum class State { Idle, PrepareDirection, Running, Cooldown };

  int directionPin;
  int runPin;
  MixerPosition target = MixerPosition::Open;
  MixerPosition current = MixerPosition::Unknown;
  MixerPosition movingTo = MixerPosition::Unknown;
  MixerPosition targetAtMoveStart = MixerPosition::Open;
  bool holding = false;          // completed pulse: stay put despite current != target
  State state = State::Idle;
  unsigned long stateMs = 0;
  unsigned long moveMs = 0;      // duration of the current move, 0 = full travel
  bool moveTowardsClose = false; // direction of the current pulse
  bool pulsePending = false;
  bool pulseTowardsClose = false;
  uint32_t pulseSeconds = 0;
  // Position estimate in seconds from the open end stop (0 = open,
  // travelTimeSeconds = closed); valid only after a completed full travel
  float estimatedSeconds = 0.0f;
  bool estimateValid = false;

  static unsigned long fullTravelMs() { return (unsigned long)travelTimeSeconds * 1000UL * 115UL / 100UL; }

  uint32_t clampPulseToRemainingTravel(bool towardsClose, uint32_t seconds) const {
    if (!estimateValid) return seconds;
    float remaining = towardsClose ? ((float)travelTimeSeconds - estimatedSeconds) : estimatedSeconds;
    if (remaining <= 0.0f) return 0;
    if ((float)seconds > remaining) return (uint32_t)(remaining + 0.5f);
    return seconds;
  }

  void setDirection(bool towardsClose) {
    // De-energized (NC) = direction OPEN, so any electrical failure defaults to open
    if (towardsClose) {
      relayOn(directionPin);
    } else {
      relayOff(directionPin);
    }
  }

  // Open-drain drive: LOW = sink to GND = relay ON, HIGH = high-impedance = relay OFF
  static void relayOn(int pin) { digitalWrite(pin, LOW); }
  static void relayOff(int pin) { digitalWrite(pin, HIGH); }
};

MixerActuator mixers[] = {
    MixerActuator("Mischer_FBHZ", MIXER1_DIRECTION_PIN, MIXER1_RUN_PIN),
    MixerActuator("Mischer_HK", MIXER2_DIRECTION_PIN, MIXER2_RUN_PIN),
};
const int mixerCount = sizeof(mixers) / sizeof(mixers[0]);

// ---------------------------------------------------------------------------
// DS18B20 temperature buses
// ---------------------------------------------------------------------------
OneWire oneWireBus1(ONEWIRE_BUS1_PIN);
OneWire oneWireBus2(ONEWIRE_BUS2_PIN);
OneWire oneWireBus3(ONEWIRE_BUS3_PIN);
DallasTemperature temperatureBuses[ONEWIRE_BUS_COUNT] = {
    DallasTemperature(&oneWireBus1),
    DallasTemperature(&oneWireBus2),
    DallasTemperature(&oneWireBus3),
};
static bool temperatureConversionRequested = false;
static unsigned long temperatureRequestMs = 0;
static unsigned long lastTemperatureCycleMs = 0;

String formatSensorAddress(const DeviceAddress& address) {
  String result = "";
  for (uint8_t i = 0; i < 8; i++) {
    if (address[i] < 0x10) result += "0";
    result += String(address[i], HEX);
    if (i < 7) result += "-";
  }
  result.toUpperCase();
  return result;
}

String getCurrentTimestamp() {
  timeClient.update();
  unsigned long epochTime = timeClient.getEpochTime();
  struct tm* ptm = gmtime((time_t*)&epochTime);
  char timestamp[25];
  sprintf(timestamp, "%04d-%02d-%02d %02d:%02d:%02d",
          ptm->tm_year + 1900, ptm->tm_mon + 1, ptm->tm_mday,
          ptm->tm_hour, ptm->tm_min, ptm->tm_sec);
  return String(timestamp);
}

// ---------------------------------------------------------------------------
// MQTT publishing
// ---------------------------------------------------------------------------
void publishMixerState(MixerActuator& mixer) {
  if (!mqttClient) return;
  JsonDocument doc;
  doc["position"] = positionToString(mixer.getPosition());
  doc["target"] = positionToString(mixer.getTarget());
  doc["moving"] = mixer.isMoving();
  if (mixer.hasPositionEstimate()) {
    doc["openPercent"] = mixer.getOpenPercent();
  }
  doc["timestamp"] = getCurrentTimestamp();
  String jsonOutput;
  serializeJson(doc, jsonOutput);
  String topic = "daten/Heizung/" + location + "/Mischersteuerung/" + mixer.name;
  mqttClient->publish(topic.c_str(), jsonOutput.c_str(), true, 1);
}

void publishDiscoveredSensors() {
  if (!mqttClient) return;
  JsonDocument doc;
  JsonArray buses = doc["buses"].to<JsonArray>();
  for (int b = 0; b < ONEWIRE_BUS_COUNT; b++) {
    JsonObject bus = buses.add<JsonObject>();
    bus["bus"] = b + 1;
    JsonArray sensors = bus["sensors"].to<JsonArray>();
    int count = temperatureBuses[b].getDeviceCount();
    for (int i = 0; i < count; i++) {
      DeviceAddress address;
      if (temperatureBuses[b].getAddress(address, i)) {
        String addressString = formatSensorAddress(address);
        JsonObject sensor = sensors.add<JsonObject>();
        sensor["address"] = addressString;
        auto it = sensorNames.find(addressString);
        sensor["name"] = (it != sensorNames.end()) ? it->second : "";
      }
    }
  }
  String jsonOutput;
  serializeJson(doc, jsonOutput);
  mqttClient->publish(("meta/MixerController/" + location + "/sensors").c_str(), jsonOutput.c_str(), true, 1);
}

void publishTemperatures() {
  for (int b = 0; b < ONEWIRE_BUS_COUNT; b++) {
    int count = temperatureBuses[b].getDeviceCount();
    for (int i = 0; i < count; i++) {
      DeviceAddress address;
      if (!temperatureBuses[b].getAddress(address, i)) continue;
      float celsius = temperatureBuses[b].getTempC(address);
      // Skip disconnected sensors and the 85.0 power-on default value
      if (celsius == DEVICE_DISCONNECTED_C || celsius == 85.0f) continue;
      String addressString = formatSensorAddress(address);
      auto it = sensorNames.find(addressString);
      String sensorDisplayName = (it != sensorNames.end()) ? it->second : addressString;
      char tempString[8];
      dtostrf(celsius, 1, 2, tempString);
      String topic = "daten/temperatur/" + location + "/" + sensorDisplayName;
      mqttClient->publish(topic.c_str(), String(tempString), true, 2);
    }
  }
}

// ---------------------------------------------------------------------------
// Configuration handling
// ---------------------------------------------------------------------------
bool updateConfiguration(const String& jsonConfig) {
  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, jsonConfig);
  if (error) {
    Serial.println("Failed to parse configuration JSON: " + String(error.c_str()));
    return false;
  }

  if (doc["Location"].is<String>()) location = doc["Location"].as<String>();
  if (doc["TravelTimeSeconds"].is<uint32_t>()) travelTimeSeconds = doc["TravelTimeSeconds"].as<uint32_t>();
  if (doc["TemperatureIntervalSeconds"].is<uint32_t>()) temperatureIntervalSeconds = doc["TemperatureIntervalSeconds"].as<uint32_t>();

  if (doc["Sensors"].is<JsonArray>()) {
    sensorNames.clear();
    for (JsonObject sensor : doc["Sensors"].as<JsonArray>()) {
      if (sensor["Address"].is<String>() && sensor["Name"].is<String>()) {
        String address = sensor["Address"].as<String>();
        address.toUpperCase();
        sensorNames[address] = sensor["Name"].as<String>();
      }
    }
  }

  Serial.println("Configuration updated: location=" + location +
                 ", travelTime=" + String(travelTimeSeconds) + "s" +
                 ", sensors=" + String(sensorNames.size()));
  return true;
}

// ---------------------------------------------------------------------------
// MQTT callback
// ---------------------------------------------------------------------------
void mqttCallback(String& topic, String& payload) {
  if (topic.startsWith("commands/MixerController/")) {
    // commands/MixerController/{location}/{mixerName} from the RulesEngine:
    // - "open" | "close" (retained): full travel to the end stop
    // - "open:N" | "close:N" (not retained): pulse N seconds towards that end,
    //   used by the step controller to hold intermediate positions
    String mixerName = topic.substring(topic.lastIndexOf('/') + 1);
    mixerName.trim();  // tolerate stray whitespace in manually published topics
    String command = payload;
    command.trim();
    command.toLowerCase();
    int separator = command.indexOf(':');
    String action = separator >= 0 ? command.substring(0, separator) : command;
    long seconds = separator >= 0 ? command.substring(separator + 1).toInt() : 0;
    for (int i = 0; i < mixerCount; i++) {
      if (mixerName == mixers[i].name) {
        if (separator < 0 && action == "open") mixers[i].setTarget(MixerPosition::Open);
        else if (separator < 0 && action == "close") mixers[i].setTarget(MixerPosition::Closed);
        else if (separator >= 0 && (action == "open" || action == "close") &&
                 seconds > 0 && seconds <= (long)travelTimeSeconds) {
          mixers[i].requestPulse(action == "close", (uint32_t)seconds);
        } else {
          Serial.println("Invalid mixer command '" + command + "'. Use open|close|open:N|close:N.");
        }
        return;
      }
    }
    Serial.println("Unknown mixer '" + mixerName + "'");
    return;
  }

  Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

  if (topic == mqtt_ConfigTopic) {
    if (updateConfiguration(payload)) {
      publishDiscoveredSensors();
    }
    return;
  }

  if (topic == mqtt_OTAtopic) {
    if (otaInProgress || !otaEnable) {
      if (otaInProgress) Serial.println("OTA in progress, ignoring message");
      if (!otaEnable) Serial.println("OTA disabled, ignoring message");
      return;
    }
    String updateVersion = AzureOTAUpdater::ExtractVersionFromUrl(payload);
    Serial.println("Current firmware version is " + String(version));
    Serial.println("New firmware version is " + updateVersion);
    if (strcmp(version, updateVersion.c_str())) {
      const char* firmwareUrl = payload.c_str();
      Serial.println("New firmware available, starting OTA Update from " + String(firmwareUrl));
      otaInProgress = true;
      if (AzureOTAUpdater::UpdateFirmwareFromUrl(firmwareUrl)) {
        Serial.println("OTA Update successfully initiated, waiting to be finished");
      }
    } else {
      Serial.println("Firmware is up to date");
    }
    return;
  }

  Serial.println("Unknown topic, ignoring message");
}

void connectToMQTT(bool cleanSession) {
  Serial.print("WiFi Status: ");
  Serial.println(WiFi.status() == WL_CONNECTED ? "Connected" : "Disconnected");

  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("WiFi not connected, attempting to reconnect...");
    wifiLib.connect();
  }
  mqttClient->connect(cleanSession);
  mqttClient->subscribe({mqtt_ConfigTopic, mqtt_OTAtopic, mqtt_CommandsTopic});
  Serial.println("MQTT Client is connected");
  Serial.println("Config Topic: " + mqtt_ConfigTopic);
  Serial.println("OTA Topic: " + mqtt_OTAtopic);
  Serial.println("Commands Topic: " + mqtt_CommandsTopic);
}

void setup() {
  Serial.begin(115200);
  Serial.print("MixerController Version:");
  Serial.println(version);
  Serial.println("-------------------------------------------------------");

  // Relay outputs first — ensure everything is released before anything else
  for (int i = 0; i < mixerCount; i++) {
    mixers[i].begin();
  }

  uint8_t mac[6];
  esp_read_mac(mac, ESP_MAC_WIFI_STA);
  chipID = "";
  for (int i = 0; i < 6; ++i) {
    if (mac[i] < 0x10) chipID += "0";
    chipID += String(mac[i], HEX);
  }
  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);
  mqtt_ConfigTopic += chipID;

  for (int b = 0; b < ONEWIRE_BUS_COUNT; b++) {
    temperatureBuses[b].begin();
    temperatureBuses[b].setWaitForConversion(false);
    Serial.println("OneWire bus " + String(b + 1) + ": " + String(temperatureBuses[b].getDeviceCount()) + " sensors found");
  }

  Serial.print("Connecting to WiFi ");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();

  Serial.println("Starting NTP client...");
  timeClient.begin();
  timeClient.update();
  Serial.println("NTP time synchronized: " + getCurrentTimestamp());

  String mqttClientID = "ESP32MixerControllerClient_" + chipID;
  mqttClient = std::unique_ptr<MQTTClientLib>(new MQTTClientLib(mqtt_broker, mqtt_port, mqttClientID, wifiClient, mqttCallback));
  connectToMQTT(true);

  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  mqttClient->publish(("meta/MixerController/" + location + "/version").c_str(), String(version), true, 0);
  publishDiscoveredSensors();

  // Both mixers start with position Unknown and target Open,
  // so the state machine performs a reference run to open automatically.
  // The retained command from the RulesEngine arrives right after the
  // subscription and takes over from there.
}

void loop() {
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();
  if (otaInProgress) {
    delay(500);
    return;
  }

  timeClient.update();

  if (!mqttClient->loop()) {
    Serial.println("MQTT Client not connected, reconnecting in loop...");
    connectToMQTT(false);
  }

  for (int i = 0; i < mixerCount; i++) {
    if (mixers[i].update()) {
      publishMixerState(mixers[i]);
    }
  }

  // Non-blocking temperature cycle: request conversion, read after >750ms
  unsigned long now = millis();
  if (!temperatureConversionRequested &&
      now - lastTemperatureCycleMs >= (unsigned long)temperatureIntervalSeconds * 1000UL) {
    for (int b = 0; b < ONEWIRE_BUS_COUNT; b++) {
      temperatureBuses[b].requestTemperatures();
    }
    temperatureConversionRequested = true;
    temperatureRequestMs = now;
  }
  if (temperatureConversionRequested && now - temperatureRequestMs >= 1000) {
    publishTemperatures();
    temperatureConversionRequested = false;
    lastTemperatureCycleMs = now;
  }

  delay(50);
}
