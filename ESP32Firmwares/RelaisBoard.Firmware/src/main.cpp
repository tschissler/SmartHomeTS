#include <Arduino.h>
#include <ArduinoJson.h>
#include <map>
#include <set>
#include <NTPClient.h>
#include <time.h>
#include <Preferences.h>
#include <esp_idf_version.h>
#include <esp_system.h>
#include <esp_task_wdt.h>

#include <esp_mac.h>
#include "AzureOTAUpdater.h"
#include "MQTTClientLib.h"
#include "WifiLib.h"

#define NUMBER_OF_RELAYS 8
#define RELAIS_1 19  
#define RELAIS_2 18
#define RELAIS_3 5
#define RELAIS_4 17
#define RELAIS_5 16
#define RELAIS_6 4
#define RELAIS_7 0
#define RELAIS_8 15

// The solid state relay board switches on a LOW level
#define RELAY_ON  LOW
#define RELAY_OFF HIGH

// Timing of the main loop and its periodic jobs
static const uint32_t LOOP_INTERVAL_MS          = 50;
static const uint32_t MQTT_RETRY_MIN_MS         = 2000;
static const uint32_t MQTT_RETRY_MAX_MS         = 60000;
static const uint32_t BROKER_PROBE_TIMEOUT_MS   = 3000;
static const uint32_t SUBSCRIBE_RETRY_MS        = 5000;
static const uint32_t WIFI_RECOVER_AFTER_MS     = 30000;
static const uint32_t HEARTBEAT_INTERVAL_MS     = 60000;
static const uint32_t STATE_REPUBLISH_MS        = 300000;
static const uint32_t OTA_RETRY_COOLDOWN_MS     = 600000;
static const uint32_t SELFTEST_STEP_MS          = 100;

// Last line of defence: if the loop is stuck (blocking library call) the task watchdog
// reboots the board, if MQTT stays unreachable for this long we reboot deliberately.
static const uint32_t WATCHDOG_TIMEOUT_MS       = 180000;
static const uint32_t MQTT_STALE_REBOOT_MS      = 900000;

static const char* NVS_NAMESPACE  = "relaisboard";
static const char* NVS_KEY_NAME   = "sensorname";
static const char* NVS_KEY_CONFIG = "roomconfig";
static const char* NVS_KEY_STATE  = "relaystate";

const char* version = FIRMWARE_VERSION;
String chipID = "";

// Room to pin mapping (will be populated from MQTT config)
std::map<String, int> roomToPinMapping;

// Desired relay state per room, mirrored to NVS so a reboot restores the valve positions
std::map<String, bool> desiredRelayState;

// Rooms whose state still needs to be published (never publish from the MQTT callback)
std::set<String> pendingStatePublishes;

// Array of available relay pins for easier management
const int relayPins[NUMBER_OF_RELAYS] = {
  RELAIS_1, RELAIS_2, RELAIS_3, RELAIS_4, 
  RELAIS_5, RELAIS_6, RELAIS_7, RELAIS_8
};

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);

WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP, "pool.ntp.org", 3600, 60000); // UTC+1, update every 60 seconds
std::unique_ptr<MQTTClientLib> mqttClient = nullptr;
Preferences preferences;

static bool otaEnable = true;
static bool otaStarted = false;
static String otaPendingUrl = "";
static uint32_t otaBlockedUntilMs = 0;
static uint32_t otaFailures = 0;

static String sensorName = "";
static String location = "";
const String mqtt_broker = "mosquitto.intern";
const int mqtt_port = 1883;
static String mqtt_OTAtopic = "OTAUpdate/Relaismodule";
static String mqtt_SensornameTopic = "config/Relaismodule/Sensorname/";
static String mqtt_config_Base = "config/Relaismodule/";
static String mqtt_config_Topic = "";
static String mqtt_CommandsTopic = "commands/Heating/#";
static String mqtt_Data_Topic = "";
static String site = "";
// Must match the OTA topic segment (OTAUpdate/Relaismodule) so a consumer can pair the
// running version with the version currently offered for this device type.
static const char* DEVICE_TYPE = "Relaismodule";
// Heartbeats used to live here before the shared status/ tree existed. Cleared once
// after an update so the stale retained message does not linger as a phantom device.
static String legacyStatusTopic = "";
static bool legacyStatusTopicCleared = false;

// Subscription bookkeeping. Every flag is cleared on every (re)connect and is only set
// once the broker has actually acknowledged the subscription.
static bool subscribedToSensorname = false;
static bool subscribedToOta = false;
static bool subscribedToConfig = false;
static bool subscribedToCommands = false;
static uint32_t nextSubscribeAttemptMs = 0;

static bool watchdogActive = false;
static bool mqttConnected = false;
static uint32_t mqttRetryDelayMs = MQTT_RETRY_MIN_MS;
static uint32_t nextMqttAttemptMs = 0;
static uint32_t lastMqttOkMs = 0;
static uint32_t lastWifiOkMs = 0;
static uint32_t lastHeartbeatMs = 0;
static uint32_t lastStateRepublishMs = 0;
static uint32_t mqttReconnects = 0;
static uint32_t wifiReconnects = 0;

// ---------------------------------------------------------------------------
// Watchdog
// ---------------------------------------------------------------------------

// millis() wraps around after ~49 days. Comparing the signed difference keeps every
// deadline check correct across the wrap, a plain "millis() < deadline" would not.
bool deadlineReached(uint32_t deadlineMs) {
  return (int32_t)(millis() - deadlineMs) >= 0;
}

void feedWatchdog() {
  if (watchdogActive) {
    esp_task_wdt_reset();
  }
}

void setupWatchdog() {
#if ESP_IDF_VERSION_MAJOR >= 5
  esp_task_wdt_config_t config = {
    .timeout_ms = WATCHDOG_TIMEOUT_MS,
    .idle_core_mask = 0,
    .trigger_panic = true
  };
  esp_err_t err = esp_task_wdt_init(&config);
  if (err == ESP_ERR_INVALID_STATE) {
    // The Arduino core already initialised the watchdog with a much shorter timeout
    err = esp_task_wdt_reconfigure(&config);
  }
#else
  esp_err_t err = esp_task_wdt_init(WATCHDOG_TIMEOUT_MS / 1000, true);
#endif

  if (err == ESP_OK && esp_task_wdt_add(NULL) == ESP_OK) {
    watchdogActive = true;
    Serial.println("Task watchdog armed with " + String(WATCHDOG_TIMEOUT_MS / 1000) + "s timeout");
  } else {
    Serial.println("Failed to arm task watchdog, error " + String(err));
  }
}

String getResetReason() {
  switch (esp_reset_reason()) {
    case ESP_RST_POWERON:  return "poweron";
    case ESP_RST_EXT:      return "external";
    case ESP_RST_SW:       return "software";
    case ESP_RST_PANIC:    return "panic";
    case ESP_RST_INT_WDT:  return "interrupt_watchdog";
    case ESP_RST_TASK_WDT: return "task_watchdog";
    case ESP_RST_WDT:      return "other_watchdog";
    case ESP_RST_BROWNOUT: return "brownout";
    default:               return "unknown";
  }
}

// ---------------------------------------------------------------------------
// Relay handling
// ---------------------------------------------------------------------------

int getRelayPinByNumber(int pinNumber) {
  if (pinNumber >= 1 && pinNumber <= NUMBER_OF_RELAYS) {
    return relayPins[pinNumber - 1]; 
  }
  return -1; // Invalid pin number
}

int getRelayPinForRoom(const String& roomName) {
  auto it = roomToPinMapping.find(roomName);
  if (it != roomToPinMapping.end()) {
    return it->second;
  }
  return -1; // Room not found
}

void initializeRelayPins() {
  // Drive the outputs to the off level *before* switching them to OUTPUT, otherwise every
  // relay is energized for the whole duration of setup(). Note that RELAIS_7 sits on GPIO0,
  // a strapping pin: pulling it low while the ESP32 resets puts the chip into download mode.
  for (int i = 0; i < NUMBER_OF_RELAYS; i++) {
    digitalWrite(relayPins[i], RELAY_OFF);
    pinMode(relayPins[i], OUTPUT);
    digitalWrite(relayPins[i], RELAY_OFF);
  }
}

// Pulses every relay once on boot. The valve LEDs are the only local feedback that the
// board has restarted, so this runs before the stored state is restored on top of it.
void runRelaySelfTest() {
  Serial.println("Running relay self test...");
  for (int i = 0; i < NUMBER_OF_RELAYS; i++) {
    digitalWrite(relayPins[i], RELAY_ON);
    delay(SELFTEST_STEP_MS);
  }
  for (int i = 0; i < NUMBER_OF_RELAYS; i++) {
    digitalWrite(relayPins[i], RELAY_OFF);
    delay(SELFTEST_STEP_MS);
  }
}

void persistDesiredState() {
  JsonDocument doc;
  for (const auto& entry : desiredRelayState) {
    doc[entry.first] = entry.second;
  }

  String serialized;
  serializeJson(doc, serialized);

  // Only touch NVS when something actually changed to keep the flash wear low
  if (preferences.getString(NVS_KEY_STATE, "") != serialized) {
    preferences.putString(NVS_KEY_STATE, serialized);
  }
}

void restoreDesiredState() {
  String stored = preferences.getString(NVS_KEY_STATE, "");
  if (stored.isEmpty()) {
    return;
  }

  JsonDocument doc;
  if (deserializeJson(doc, stored)) {
    Serial.println("Stored relay state is corrupt, ignoring it");
    return;
  }

  for (JsonPair entry : doc.as<JsonObject>()) {
    desiredRelayState[String(entry.key().c_str())] = entry.value().as<bool>();
  }
  Serial.println("Restored desired state for " + String(desiredRelayState.size()) + " rooms from NVS");
}

// Applies the desired state to every mapped relay. Rooms without a known desired state
// stay off, which is the safe position for a heating valve.
void applyDesiredState() {
  for (const auto& mapping : roomToPinMapping) {
    auto it = desiredRelayState.find(mapping.first);
    bool on = (it != desiredRelayState.end()) && it->second;
    digitalWrite(mapping.second, on ? RELAY_ON : RELAY_OFF);
    pendingStatePublishes.insert(mapping.first);
  }
}

void setRelayForRoom(const String& roomName, bool on) {
  int relayPin = getRelayPinForRoom(roomName);
  if (relayPin == -1) {
    return;
  }

  digitalWrite(relayPin, on ? RELAY_ON : RELAY_OFF);
  desiredRelayState[roomName] = on;
  persistDesiredState();
  pendingStatePublishes.insert(roomName);

  Serial.println("Relay for room '" + roomName + "' turned " + (on ? "ON" : "OFF") +
                 " (GPIO " + String(relayPin) + ")");
}

bool updateRoomMapping(const String& jsonConfig) {
  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, jsonConfig);
  
  if (error) {
    Serial.println("Failed to parse room mapping JSON: " + String(error.c_str()));
    return false;
  }
  
  if (!doc.is<JsonArray>()) {
    Serial.println("Room mapping JSON must be an array");
    return false;
  }

  std::map<String, int> newMapping;
  JsonArray rooms = doc.as<JsonArray>();
  
  for (JsonObject room : rooms) {
    if (!room["Room"].is<String>() || !room["Pin"].is<int>()) {
      Serial.println("Room mapping entry missing 'Room' or 'Pin' field");
      continue;
    }
    
    String roomName = room["Room"].as<String>();
    int pinNumber = room["Pin"].as<int>();
    
    int actualPin = getRelayPinByNumber(pinNumber);
    if (actualPin == -1) {
      Serial.println("Invalid pin number " + String(pinNumber) + " for room " + roomName);
      continue;
    }
    
    newMapping[roomName] = actualPin;
    Serial.println("Mapped room '" + roomName + "' to pin " + String(pinNumber) + " (GPIO " + String(actualPin) + ")");
  }

  if (newMapping.empty()) {
    Serial.println("Room mapping contains no usable entry, keeping the previous one");
    return false;
  }

  // Release relays that are no longer mapped so they cannot stay stuck in the on position
  for (const auto& mapping : roomToPinMapping) {
    if (newMapping.find(mapping.first) == newMapping.end()) {
      digitalWrite(mapping.second, RELAY_OFF);
    }
  }

  roomToPinMapping = newMapping;
  applyDesiredState();

  Serial.println("Room mapping updated with " + String(roomToPinMapping.size()) + " rooms");
  return true;
}

// ---------------------------------------------------------------------------
// Time
// ---------------------------------------------------------------------------

String getCurrentTimestamp() {
  // No update() call here: the main loop keeps the NTP client fresh and update() may block
  unsigned long epochTime = timeClient.getEpochTime();
  if (epochTime < 1600000000UL) {
    return "";
  }

  time_t rawTime = (time_t)epochTime;
  struct tm *ptm = gmtime(&rawTime);
  
  char timestamp[25];
  sprintf(timestamp, "%04d-%02d-%02d %02d:%02d:%02d",
          ptm->tm_year + 1900, ptm->tm_mon + 1, ptm->tm_mday,
          ptm->tm_hour, ptm->tm_min, ptm->tm_sec);
  
  return String(timestamp);
}

// ---------------------------------------------------------------------------
// MQTT publishing
// ---------------------------------------------------------------------------

void publishRelayState(const String& roomName) {
  if (!mqttConnected || mqtt_Data_Topic.isEmpty()) {
    return;
  }

  auto it = desiredRelayState.find(roomName);
  bool on = (it != desiredRelayState.end()) && it->second;

  JsonDocument doc;
  doc["state"] = on ? "ON" : "OFF";
  doc["timestamp"] = getCurrentTimestamp();
  
  String jsonOutput;
  serializeJson(doc, jsonOutput);
  
  mqttClient->publish(mqtt_Data_Topic + roomName, jsonOutput, true, 1, false);
}

void publishPendingStates() {
  if (!mqttConnected || pendingStatePublishes.empty()) {
    return;
  }

  // One room per loop iteration keeps the publish (QoS 1, waits for PUBACK) off the
  // critical path even when the whole state is republished at once.
  auto it = pendingStatePublishes.begin();
  String roomName = *it;
  pendingStatePublishes.erase(it);
  publishRelayState(roomName);
}

void publishHeartbeat() {
  if (!mqttConnected || sensorName.isEmpty()) {
    return;
  }

  // Fields the shared heartbeat does not know about. Serialized through ArduinoJson and
  // stripped of its outer braces, so room names are escaped properly.
  JsonDocument doc;
  doc["wifiReconnects"] = wifiReconnects;
  doc["otaFailures"] = otaFailures;

  JsonObject relays = doc["relays"].to<JsonObject>();
  for (const auto& mapping : roomToPinMapping) {
    auto it = desiredRelayState.find(mapping.first);
    relays[mapping.first] = ((it != desiredRelayState.end()) && it->second) ? "ON" : "OFF";
  }

  String extraFields;
  serializeJson(doc, extraFields);
  extraFields = extraFields.substring(1, extraFields.length() - 1);

  mqttClient->publishStatus(site, DEVICE_TYPE, sensorName, String(version),
                            getCurrentTimestamp(), extraFields);
}

// ---------------------------------------------------------------------------
// MQTT subscriptions
// ---------------------------------------------------------------------------

void applySensorName(const String& name) {
  if (name.isEmpty()) {
    return;
  }

  sensorName = name;
  location = name;
  location.replace("Relaismodule_", "");

  String newConfigTopic = mqtt_config_Base + sensorName + "/Relais";
  if (newConfigTopic != mqtt_config_Topic) {
    if (!mqtt_config_Topic.isEmpty() && mqttConnected) {
      mqttClient->unsubscribe(mqtt_config_Topic);
    }
    mqtt_config_Topic = newConfigTopic;
    subscribedToConfig = false;
  }

  mqtt_Data_Topic = "daten/Heizung/" + location + "/FussbodenHeizungSteuerung";
  legacyStatusTopic = "meta/" + sensorName + "/status/RelaisModule";

  // location is "M1_EG"; the status tree groups by site, so strip the floor
  site = location;
  int floorSeparator = site.indexOf('_');
  if (floorSeparator > 0) {
    site = site.substring(0, floorSeparator);
  }

  Serial.println("Sensor name set to: " + sensorName);
  Serial.println("Location set to: " + location);
  Serial.println("Site set to: " + site);
  Serial.println("Config topic set to: " + mqtt_config_Topic);

  if (preferences.getString(NVS_KEY_NAME, "") != sensorName) {
    preferences.putString(NVS_KEY_NAME, sensorName);
  }
}

void resetSubscriptions() {
  subscribedToSensorname = false;
  subscribedToOta = false;
  subscribedToConfig = false;
  subscribedToCommands = false;
  nextSubscribeAttemptMs = millis();
}

// Re-establishes every subscription that is currently missing. A flag is only latched
// once the broker acknowledged the SUBSCRIBE, so a failed attempt is retried instead of
// silently leaving the board deaf.
void syncSubscriptions() {
  if (!mqttConnected) {
    return;
  }
  if (subscribedToSensorname && subscribedToOta && subscribedToConfig && subscribedToCommands) {
    return;
  }
  if (!deadlineReached(nextSubscribeAttemptMs)) {
    return;
  }
  nextSubscribeAttemptMs = millis() + SUBSCRIBE_RETRY_MS;

  if (!subscribedToSensorname) {
    subscribedToSensorname = mqttClient->subscribe(mqtt_SensornameTopic);
  }
  if (!subscribedToOta) {
    subscribedToOta = mqttClient->subscribe(mqtt_OTAtopic);
  }
  if (!subscribedToConfig && !mqtt_config_Topic.isEmpty()) {
    subscribedToConfig = mqttClient->subscribe(mqtt_config_Topic);
  }
  if (!subscribedToCommands && !roomToPinMapping.empty()) {
    subscribedToCommands = mqttClient->subscribe(mqtt_CommandsTopic);
  }
}

// ---------------------------------------------------------------------------
// MQTT callback
// ---------------------------------------------------------------------------

void mqttCallback(String &topic, String &payload) {
    Serial.println("Message arrived on topic: " + topic + ". Message: " + payload);

    // Handle room configuration updates
    if (topic == mqtt_config_Topic) {
      Serial.println("Received room configuration update");
      if (updateRoomMapping(payload)) {
        preferences.putString(NVS_KEY_CONFIG, payload);
        Serial.println("Room mapping configuration updated successfully");
      } else {
        Serial.println("Failed to update room mapping configuration");
      }
      return;
    }

    // Handle heating commands with dynamic room mapping
    if (topic.startsWith("commands/Heating/")) {
      String roomName = topic.substring(String("commands/Heating/").length());
      if (getRelayPinForRoom(roomName) == -1) {
        // The wildcard subscription also receives the rooms of the other relay boards
        return;
      }

      String payloadUpper = payload;
      payloadUpper.toUpperCase();
      if (payloadUpper == "ON") {
          setRelayForRoom(roomName, true);
      } else if (payloadUpper == "OFF") {
          setRelayForRoom(roomName, false);
      } else {
          Serial.println("Invalid payload for relay command. Use 'ON' or 'OFF'.");
      }
      return;
    }

    if (topic == mqtt_SensornameTopic) {
      applySensorName(payload);
      return;
    } 

    if (topic == mqtt_OTAtopic) {
      if (!otaEnable) {
        Serial.println("OTA disabled, ignoring message");
        return;
      }
      if (otaStarted) {
        Serial.println("OTA in progress, ignoring message");
        return;
      }
      if (!deadlineReached(otaBlockedUntilMs)) {
        Serial.println("OTA is in cooldown after a failed update, ignoring message");
        return;
      }
  
      String updateVersion = AzureOTAUpdater::ExtractVersionFromUrl(payload);
      Serial.println("Current firmware version is " + String(version));
      Serial.println("New firmware version is " + updateVersion);
      if(strcmp(version, updateVersion.c_str())) {
          // Remember the request only - starting the download from inside the MQTT
          // callback would run it while the MQTT client is parsing an incoming packet
          otaPendingUrl = payload;
      }
      else {
        Serial.println("Firmware is up to date");
      }
      return;
    }

    Serial.println("Unknown topic, ignoring message");
}

// ---------------------------------------------------------------------------
// OTA
// ---------------------------------------------------------------------------

// Returns true while an update is running and the rest of the loop has to stand back.
bool handleOta() {
  if (otaStarted) {
    int status = AzureOTAUpdater::CheckUpdateStatus();
    if (status == 1) {
      return true;
    }

    // Anything else means the update is over. HttpsOTA keeps reporting FAIL forever, so
    // the status must only be read while an update of ours is actually running.
    otaStarted = false;
    if (status < 0) {
      otaFailures++;
      otaBlockedUntilMs = millis() + OTA_RETRY_COOLDOWN_MS;
      Serial.println("OTA update failed, resuming normal operation");
    }
    return false;
  }

  if (otaPendingUrl.isEmpty()) {
    return false;
  }

  String url = otaPendingUrl;
  otaPendingUrl = "";
  Serial.println("New firmware available, starting OTA Update from " + url);
  otaStarted = AzureOTAUpdater::UpdateFirmwareFromUrl(url.c_str());
  return otaStarted;
}

// ---------------------------------------------------------------------------
// Connectivity
// ---------------------------------------------------------------------------

void ensureWifiConnected() {
  if (WiFi.status() == WL_CONNECTED) {
    lastWifiOkMs = millis();
    return;
  }

  // Give the auto-reconnect of the WiFi stack a chance before taking over
  if (millis() - lastWifiOkMs < WIFI_RECOVER_AFTER_MS) {
    return;
  }

  Serial.println("WiFi has been down for more than " + String(WIFI_RECOVER_AFTER_MS / 1000) +
                 "s, rescanning and reconnecting...");
  wifiReconnects++;
  WiFi.disconnect(false);

  // The rescan is essential: connect() pins itself to the BSSID picked at boot time and
  // would otherwise keep retrying an access point that is no longer there.
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();
  lastWifiOkMs = millis();
}

// A plain TCP probe before handing over to MQTTClientLib::connect(), which loops forever
// until the broker answers. If the broker is down we stay in the loop and keep the
// watchdog, the heartbeat and the serial diagnostics alive.
bool isBrokerReachable() {
  WiFiClient probe;
  bool reachable = probe.connect(mqtt_broker.c_str(), mqtt_port, BROKER_PROBE_TIMEOUT_MS);
  probe.stop();
  return reachable;
}

void connectToMQTT() {
  if (!deadlineReached(nextMqttAttemptMs)) {
    return;
  }

  if (WiFi.status() != WL_CONNECTED) {
    nextMqttAttemptMs = millis() + MQTT_RETRY_MIN_MS;
    return;
  }

  if (!isBrokerReachable()) {
    Serial.println("MQTT broker " + mqtt_broker + " is not reachable, retrying in " +
                   String(mqttRetryDelayMs / 1000) + "s");
    nextMqttAttemptMs = millis() + mqttRetryDelayMs;
    mqttRetryDelayMs = min(mqttRetryDelayMs * 2, MQTT_RETRY_MAX_MS);
    return;
  }

  // Always connect with a clean session and rebuild every subscription ourselves. Relying
  // on a broker side session silently loses all subscriptions whenever Mosquitto restarts.
  mqttClient->connect(true);
  mqttConnected = true;
  mqttReconnects++;
  lastMqttOkMs = millis();
  mqttRetryDelayMs = MQTT_RETRY_MIN_MS;
  resetSubscriptions();
  syncSubscriptions();

  Serial.println("MQTT Client is connected (reconnect #" + String(mqttReconnects) + ")");

  if (!sensorName.isEmpty()) {
    mqttClient->publish("meta/" + sensorName + "/version/RelaisModule", String(version), true, 0);
  }

  if (!legacyStatusTopicCleared && !legacyStatusTopic.isEmpty()) {
    mqttClient->publish(legacyStatusTopic, "", true, 0, false);
    legacyStatusTopicCleared = true;
  }

  // Publish the current state right away so consumers see where we stand after a reconnect
  for (const auto& mapping : roomToPinMapping) {
    pendingStatePublishes.insert(mapping.first);
  }
  // Due immediately, so a reconnect is visible on the status topic right away
  lastHeartbeatMs = millis() - HEARTBEAT_INTERVAL_MS;
}

// ---------------------------------------------------------------------------
// Setup / loop
// ---------------------------------------------------------------------------

void setup() {
  Serial.begin(115200);

  initializeRelayPins();

  Serial.print("Relais Module Version:");
  Serial.println(version);
  Serial.println("Reset reason: " + getResetReason());
  Serial.println("-------------------------------------------------------");

  setupWatchdog();
  runRelaySelfTest();

  uint8_t mac[6];
  esp_read_mac(mac, ESP_MAC_WIFI_STA);

  chipID = "";
  for (int i = 0; i < 6; ++i) {
    if (mac[i] < 0x10) chipID += "0";  // Add leading zero if needed
    chipID += String(mac[i], HEX);
  }

  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);

  mqtt_SensornameTopic += chipID;

  // Restore the last known configuration and valve positions before going online, so a
  // reboot does not depend on the broker being available.
  preferences.begin(NVS_NAMESPACE, false);
  restoreDesiredState();
  applySensorName(preferences.getString(NVS_KEY_NAME, ""));
  String storedConfig = preferences.getString(NVS_KEY_CONFIG, "");
  if (!storedConfig.isEmpty()) {
    Serial.println("Restoring room mapping from NVS");
    updateRoomMapping(storedConfig);
  }

  feedWatchdog();

  // Connect to WiFi
  Serial.print("Connecting to WiFi ");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();
  lastWifiOkMs = millis();

  feedWatchdog();

  // Initialize NTP Client
  Serial.println("Starting NTP client...");
  timeClient.begin();
  timeClient.update();
  Serial.println("NTP time synchronized: " + getCurrentTimestamp());

  feedWatchdog();

  // Set up MQTT. The connection itself is established by the loop, which keeps setup()
  // from blocking forever while the broker is unavailable.
  String mqttClientID = "ESP32RelaismoduleClient_" + chipID;
  mqttClient = std::unique_ptr<MQTTClientLib>(new MQTTClientLib(mqtt_broker, mqtt_port, mqttClientID, wifiClient, mqttCallback));

  Serial.print("IP Address: ");
  Serial.println(WiFi.localIP());

  lastMqttOkMs = millis();
}

void loop() {
    feedWatchdog();

    if (handleOta()) {
      delay(500);
      return;
    }

    ensureWifiConnected();

    // Update NTP time client
    timeClient.update();

    if (mqttClient->loop()) {
      mqttConnected = true;
      lastMqttOkMs = millis();
    } else {
      if (mqttConnected) {
        Serial.println("MQTT connection lost (last error " + String(mqttClient->lastError()) + "), reconnecting...");
        mqttConnected = false;
      }
      connectToMQTT();
    }

    if (mqttConnected) {
      syncSubscriptions();
      publishPendingStates();

      if (millis() - lastHeartbeatMs >= HEARTBEAT_INTERVAL_MS) {
        lastHeartbeatMs = millis();
        publishHeartbeat();
      }

      if (millis() - lastStateRepublishMs >= STATE_REPUBLISH_MS) {
        lastStateRepublishMs = millis();
        for (const auto& mapping : roomToPinMapping) {
          pendingStatePublishes.insert(mapping.first);
        }
      }
    } else if (millis() - lastMqttOkMs >= MQTT_STALE_REBOOT_MS) {
      // Something we cannot fix from here keeps us off the broker - start over
      Serial.println("No MQTT connection for " + String(MQTT_STALE_REBOOT_MS / 60000) +
                     " minutes, restarting the board");
      Serial.flush();
      ESP.restart();
    }

    delay(LOOP_INTERVAL_MS);
}

