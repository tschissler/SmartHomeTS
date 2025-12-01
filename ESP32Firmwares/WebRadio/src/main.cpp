#include <Arduino.h>
#include <Wire.h>
#include <WiFi.h>
#include <WiFiClient.h>
#include <WiFiClientSecure.h>
#include <HTTPClient.h>
#include <LiquidCrystal_I2C.h>
#include "AudioFileSourceICYStream.h"
#include "AudioFileSourceBuffer.h"
#include "AudioGeneratorMP3.h"
#include "AudioOutputI2S.h"
#include "esp_sleep.h"
#include "ESP32Helpers.h"
#include <NTPClient.h>

// Shared libaries
#include "AzureOTAUpdater.h"
#include "WifiLib.h"

const char* version = TEMPSENSORFW_VERSION;
String chipID = "";
 
// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_PASSWORDS as environment variables on your dev-system following the pattern: WIFI_PASSWORDS="ssid1;password1|ssid2;password2"
WifiLib wifiLib(WIFI_PASSWORDS);
WiFiClient wifiClient;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP);

static int otaInProgress = 0;
static bool otaEnable = OTA_ENABLED != "false";

// WiFi credentials - CHANGE THESE!
const char* ssid = "agileMax_Guest";
const char* password = "WLAN_agileMax";

// Initialize the LCD with I2C address (usually 0x27 or 0x3F), 20 columns and 4 rows
LiquidCrystal_I2C lcd(0x27, 20, 4);

// Audio objects
AudioGeneratorMP3 *mp3;
AudioFileSourceICYStream *file;
AudioFileSourceBuffer *buff;
AudioOutputI2S *out;

// FreeRTOS task handles for dual-core operation
TaskHandle_t audioTaskHandle = NULL;

// Queue for passing metadata from audio core to UI core
QueueHandle_t metadataQueue = NULL;

// Structure for metadata messages
struct MetadataMessage {
  char line1[21];  // First line: 20 chars + null terminator
  char line2[21];  // Second line: 20 chars + null terminator
};

// KY-023 Joystick pins
#define VRX_PIN 34    // Analog pin for X-axis
#define VRY_PIN 35    // Analog pin for Y-axis
#define SW_PIN  32    // Digital pin for switch (with pull-up)

// I2S pins for internal DAC (like working code)
// GPIO25 is used automatically by AudioOutputI2S(0, 1)
// No external pins needed - uses internal DAC

// Web radio stations - Classic Rock and Metal focused
const char* stations[] = {
  "https://liveradio.swr.de/sw282p3/swr4ul/",
  "https://streams.br.de/brheimat_2.m3u",
  "https://streams.br.de/brschlager_2.m3u",
  "https://orf-live.ors-shoutcast.at/stm-q1a.m3u",
  "https://dispatcher.rndfnk.com/br/br24/live/mp3/mid",
  "https://dispatcher.rndfnk.com/br/br1/schwaben/mp3/mid",
  "https://st01.sslstream.dlf.de/dlf/01/128/mp3/stream.mp3?aggregator=web"
};

const String stationNames[] = {
  "SWR4 Ulm",
  "BR Heimat",
  "BR Schlager",
  "ORF Steiermark",
  "BR24",
  "BR1 Schwaben",
  "Deutschlandfunk"
};

const int numStations = sizeof(stations) / sizeof(stations[0]);

// Radio variables
int currentStation = 0;
bool radioPlaying = false;
bool backlightOn = true;
unsigned long lastJoystickRead = 0;
unsigned long lastAudioCheck = 0;
unsigned long streamStartTime = 0;

// Retry mechanism variables
int retryAttempt = 0;
const int maxRetries = 3;

// Deep sleep variables
bool deepSleepEnabled = true; // Enable/disable deep sleep feature
unsigned long sleepDelay = 2000; // Delay before entering deep sleep (ms)

// Function declarations
void enterDeepSleep();
const unsigned long STREAM_TIMEOUT = 10000; // 10 seconds timeout

// Robust switch debouncing variables
bool switchPressed = false;
bool lastStableState = HIGH;
bool switchReading = HIGH;
unsigned long lastDebounceTime = 0;
const unsigned long debounceDelay = 50;
const unsigned long joystickDelay = 150; // Slightly faster for better responsiveness

void updateDisplay() {
  lcd.clear();                
  
  // Line 1+2: Status symbol + station info
  lcd.setCursor(0, 0);
  
  // First position: Play/Pause symbol
  if (radioPlaying) {
    lcd.print("\x7E"); // Play symbol (right-pointing triangle)
  } else {
    lcd.print("\xFF"); // Stop/Pause symbol (solid block)
  }
  
  // Second position: Space
  lcd.print(" ");
  
  // Remaining 18 characters for station name
  String stationName = stationNames[currentStation];
  int newlineIndex = stationName.indexOf('\n');
  if (newlineIndex != -1) {
    // Multi-line station name
    String line1 = stationName.substring(0, newlineIndex);
    String line2 = stationName.substring(newlineIndex + 1);
    
    // Print first part (max 18 chars to leave room for status)
    if (line1.length() > 18) {
      lcd.print(line1.substring(0, 18));
    } else {
      lcd.print(line1);
    }
    
    lcd.setCursor(2, 1); // Start at position 2 (after status symbol and space)
    if (line2.length() > 18) {
      lcd.print(line2.substring(0, 18));
    } else {
      lcd.print(line2);
    }
  } else {
    // Single line station name
    if (stationName.length() > 18) {
      // If name is too long, print first 18 chars on line 1, rest on line 2
      lcd.print(stationName.substring(0, 18));
      lcd.setCursor(2, 1); // Start at position 2 on second line
      String remainingText = stationName.substring(18);
      if (remainingText.length() > 18) {
        lcd.print(remainingText.substring(0, 18));
      } else {
        lcd.print(remainingText);
      }
    } else {
      // Station name fits in remaining space
      lcd.print(stationName);
    }
  }
  // Note: Lines 3 and 4 are now reserved for metadata display
}

void connectToWiFi() {
  lcd.setCursor(0, 2);
  lcd.print("Verbinde W-LAN...");
  
    // Connect to WiFi
  Serial.print("Connecting to WiFi ");
  wifiLib.scanAndSelectNetwork();
  wifiLib.connect();
  String ssid = wifiLib.getSSID();

  timeClient.begin();
  timeClient.setTimeOffset(0); // Set your time offset from UTC in seconds
  timeClient.update();

}

// Hilfsfunktion: trimmt und checkt, ob Zeile wie URL aussieht
bool looksLikeUrl(const String& s) {
  return s.startsWith("http://") || s.startsWith("https://");
}


// Sehr einfache M3U/PLS-Parser: nimm die erste nicht-kommentierte URL
String extractUrlFromPlaylist(const String& body) {
  // PLS kann Zeilen wie "File1=http://..." enthalten
  int idx = body.indexOf("File1=");
  if (idx >= 0) {
    int eol = body.indexOf('\n', idx);
    String line = body.substring(idx + 6, eol < 0 ? body.length() : eol);
    line.trim();
    if (looksLikeUrl(line)) return line;
  }
  // M3U: erste Zeile, die nicht mit '#' beginnt und wie URL aussieht
  int start = 0;
  while (start >= 0 && start < (int)body.length()) {
    int eol = body.indexOf('\n', start);
    String line = body.substring(start, eol < 0 ? body.length() : eol);
    line.trim();
    if (line.length() && !line.startsWith("#") && looksLikeUrl(line)) {
      return line;
    }
    if (eol < 0) break;
    start = eol + 1;
  }
  return "";
}

// Folgt Redirects und löst Playlists auf. Liefert finale Stream-URL.
String resolveStreamUrl(String url, uint8_t maxHops = 5, uint32_t timeoutMs = 6000) {
  for (uint8_t hop = 0; hop < maxHops; ++hop) {
    // HTTPS oder HTTP?
    bool isHttps = url.startsWith("https://");
    HTTPClient http;
    int code = 0;

    if (isHttps) {
      WiFiClientSecure *client = new WiFiClientSecure;
      client->setTimeout(timeoutMs);
      // Für produktive Nutzung: Zertifikat prüfen! (hier zur Vereinfachung aus)
      client->setInsecure();
      if (!http.begin(*client, url)) { delete client; return ""; }
    } else {
      WiFiClient *client = new WiFiClient;
      client->setTimeout(timeoutMs);
      if (!http.begin(*client, url)) { delete client; return ""; }
    }

    http.setTimeout(timeoutMs);
    // Wir wollen Header sehen (Content-Type / Location)
    // Manche Server mögen HTTP/1.0 lieber, aber i. d. R. unnötig:
    // http.useHTTP10(true);

    // Nur Header reicht manchen nicht—wir machen GET mit kleinem Timeout
    const char* headerKeys[] = { "Content-Type", "Location" };
    http.collectHeaders(headerKeys, 2);
    code = http.GET();

    // Redirect?
    if (code == HTTP_CODE_MOVED_PERMANENTLY     || // 301
        code == HTTP_CODE_FOUND                 || // 302
        code == HTTP_CODE_SEE_OTHER             || // 303
        code == HTTP_CODE_TEMPORARY_REDIRECT    || // 307
        code == HTTP_CODE_PERMANENT_REDIRECT) {    // 308
      String loc = http.header("Location");
      http.end();
      if (!loc.length()) return ""; // kein Location-Header => Abbruch
      // Relative Locations sind selten bei Radios, aber sicherheitshalber:
      if (!looksLikeUrl(loc)) {
        // Primitive Behandlung: relative Location an Host anhängen
        // (für komplexe Fälle besser eine URL-Bibliothek nutzen)
        int schemeEnd = url.indexOf("://");
        if (schemeEnd < 0) return "";
        int hostStart = schemeEnd + 3;
        int pathStart = url.indexOf('/', hostStart);
        String base = (pathStart > 0) ? url.substring(0, pathStart) : url;
        if (!loc.startsWith("/")) loc = "/" + loc;
        url = base + loc;
      } else {
        url = loc;
      }
      continue; // nächster Hop
    }

    if (code == HTTP_CODE_OK) {
      String ctype = http.header("Content-Type");
      ctype.toLowerCase();

      // Playlist erkannt?
      bool isPlaylist = ctype.indexOf("audio/x-mpegurl") >= 0 ||
                        ctype.indexOf("application/vnd.apple.mpegurl") >= 0 ||
                        ctype.indexOf("application/x-mpegurl") >= 0 ||
                        ctype.indexOf("application/pls") >= 0 ||
                        ctype.indexOf("audio/x-scpls") >= 0 ||
                        url.endsWith(".m3u") || url.endsWith(".m3u8") || url.endsWith(".pls");

      if (isPlaylist) {
        String body = http.getString(); // Inhalt holen und daraus URL extrahieren
        http.end();
        String inner = extractUrlFromPlaylist(body);
        if (!inner.length()) return "";
        url = inner; // und nächste Runde – könnte wieder Redirect sein
        continue;
      }

      // Direkter Stream? Typische Content-Types: audio/mpeg (MP3), audio/aacp (AAC)
      bool isDirectStream = ctype.indexOf("audio/mpeg") >= 0 ||
                            ctype.indexOf("audio/aac")  >= 0 ||
                            ctype.indexOf("audio/aacp") >= 0 ||
                            ctype.indexOf("audio/ogg")  >= 0;

      http.end();

      if (isDirectStream) {
        return url; // finale, direkt abspielbare Stream-URL
      } else {
        // Manche Sender liefern 200 + HTML mit Meta-Redirect -> versuchen, URL aus Body zu ziehen (einfacher Heuristik-Schritt)
        // Hier: wir beenden und geben zurück, um nicht unnötig Daten zu ziehen.
        return url; // Versuch: oft ist es dennoch ein Stream-Endpunkt
      }
    }

    // Unerwarteter Statuscode
    http.end();
    return "";
  }
  // Zu viele Hops
  return "";
}

// Erzwinge HTTP statt HTTPS für die eigentliche Audio-Wiedergabe
String makeHttpUrl(String url) {
  if (url.startsWith("https://")) {
    url.remove(4, 1); // "https" -> "http"
  }
  return url;
}

// Status and metadata callback functions
void statusCB(void *cbData, int code, const char *string) {
  const char *tag = (const char *)cbData;
  Serial.printf("[%s][STATUS] code=%d msg=%s\n", tag ? tag : "?", code, string ? string : "");
}

// Function to convert German umlauts to LCD character codes
String convertUmlautsToLcd(String text) {
  // Replace UTF-8 encoded German umlauts with HD44780 LCD character codes
  text.replace("ä", "\xe1");  // ä -> LCD char 0xe1
  text.replace("Ä", "\xe1");  // Ä -> LCD char 0xe1 (same as ä on most displays)
  text.replace("ö", "\xef");  // ö -> LCD char 0xef  
  text.replace("Ö", "\xef");  // Ö -> LCD char 0xef (same as ö on most displays)
  text.replace("ü", "\xf5");  // ü -> LCD char 0xf5
  text.replace("Ü", "\xf5");  // Ü -> LCD char 0xf5 (same as ü on most displays)
  text.replace("ß", "\xe2");  // ß -> LCD char 0xe2
  
  // Also handle ISO-8859-1 encoded characters (direct byte values)
  text.replace(String((char)0xE4), "\xe1");  // ä (0xE4) -> LCD ä
  text.replace(String((char)0xC4), "\xe1");  // Ä (0xC4) -> LCD ä
  text.replace(String((char)0xF6), "\xef");  // ö (0xF6) -> LCD ö
  text.replace(String((char)0xD6), "\xef");  // Ö (0xD6) -> LCD ö
  text.replace(String((char)0xFC), "\xf5");  // ü (0xFC) -> LCD ü
  text.replace(String((char)0xDC), "\xf5");  // Ü (0xDC) -> LCD ü
  text.replace(String((char)0xDF), "\xe2");  // ß (0xDF) -> LCD ß
  
  return text;
}

void metadataCB(void *cbData, const char *type, bool isUnicode, const char *str) {
  const char *tag = (const char *)cbData;
  Serial.printf("[%s][META] %s%s: %s\n", tag ? tag : "?", isUnicode ? "(U)" : "", type ? type : "", str ? str : "");

  // Send metadata to UI core via queue instead of direct LCD write
  if (str && strlen(str) > 0 && metadataQueue != NULL) {
    MetadataMessage msg;
    memset(&msg, 0, sizeof(msg)); // Clear the structure
    
    // Convert umlauts to LCD character codes for proper display
    String metadata = convertUmlautsToLcd(String(str));
    int totalLength = metadata.length();
    
    if (totalLength <= 20) {
      // Fits in one line
      strncpy(msg.line1, metadata.c_str(), 20);
      msg.line1[20] = '\0';
      msg.line2[0] = '\0'; // Empty second line
    } else {
      // Need to wrap to two lines (max 40 chars total)
      String line1Text = metadata.substring(0, 20);
      String line2Text = metadata.substring(20, min(40, totalLength));
      
      strncpy(msg.line1, line1Text.c_str(), 20);
      msg.line1[20] = '\0';
      strncpy(msg.line2, line2Text.c_str(), 20);
      msg.line2[20] = '\0';
    }
    
    // Send to queue (non-blocking from audio core)
    xQueueSend(metadataQueue, &msg, 0);
  }
}

// Audio task that runs on Core 1 - dedicated to audio processing only
void audioTask(void *parameter) {
  Serial.println("Audio task started on Core 1");
  
  for (;;) {
    // Handle audio streaming with buffer monitoring
    if (mp3 && mp3->isRunning()) {
      if (!mp3->loop()) {
        mp3->stop();
        radioPlaying = false;
        Serial.println("Stream ended");
        // Note: updateDisplay() will be called from main loop on Core 0
      }
      
      // Buffer monitoring (every 5 seconds)
      static unsigned long lastBufferCheck = 0;
      if (millis() - lastBufferCheck > 5000) {
        lastBufferCheck = millis();
        uint32_t bufferLevel = buff ? buff->getFillLevel() : 0;
        Serial.print("Buffer: ");
        Serial.print(bufferLevel);
        Serial.print(" bytes, WiFi: ");
        Serial.print(WiFi.RSSI());
        Serial.print(" dBm, Heap: ");
        Serial.println(ESP.getFreeHeap());
        
        // Warn if buffer is getting low
        if (bufferLevel < 4096) {
          Serial.println("WARNING: Buffer level low!");
        }
      }
    } else {
      // No audio playing, yield CPU time
      vTaskDelay(100 / portTICK_PERIOD_MS);
    }
    
    // Small delay to prevent watchdog timeout
    vTaskDelay(1 / portTICK_PERIOD_MS);
  }
}

void startRadio() {
  // Turn on LCD backlight when starting audio
  if (!backlightOn) {
    lcd.backlight();
    backlightOn = true;
  }
  
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("WiFi not connected!");
    retryAttempt = 0; // Reset retry counter on WiFi failure
    return;
  }
  
  // Stop any current playback and ensure clean state
  if (mp3 && mp3->isRunning()) {
    mp3->stop();
    delay(100); // Give time for proper cleanup
  }
  
  // Also close any existing file connection
  if (file && file->isOpen()) {
    file->close();
    delay(50); // Additional cleanup time
  }
  
  Serial.print("Connecting to: ");
  Serial.println(stations[currentStation]);
  if (retryAttempt > 0) {
    Serial.print("Retry attempt: ");
    Serial.print(retryAttempt);
    Serial.print("/");
    Serial.println(maxRetries);
    
    // Show retry status on display
    lcd.setCursor(0, 3);
    lcd.print("Versuch ");
    lcd.print(retryAttempt);
    lcd.print("/");
    lcd.print(maxRetries);
    lcd.print("...        ");
  }
  
  // Animation characters for connection/resolution phase
  char animChars[] = {'-', '\\', '|', '/'};
  int animIndex = 0;
  
  // Show initial connecting animation
  lcd.setCursor(0, 0);
  lcd.print(animChars[animIndex]);
  animIndex = (animIndex + 1) % 4;
  
  // Resolve redirects and playlists with animation
  Serial.println("Resolving stream URL with redirects and playlists...");
  
  // Start resolving - this may take several seconds for redirects
  unsigned long resolveStart = millis();
  String finalUrl = "";
  
  // We'll call resolveStreamUrl in a way that allows us to show animation
  // Unfortunately resolveStreamUrl is blocking, but we can show animation before and after
  
  // Show connecting animation for a brief moment
  for (int i = 0; i < 10; i++) {
    lcd.setCursor(0, 0);
    lcd.print(animChars[animIndex]);
    animIndex = (animIndex + 1) % 4;
    delay(50);
  }
  
  // Now resolve the actual URL (this is the blocking part)
  finalUrl = resolveStreamUrl(stations[currentStation], 6, 7000);
  
  if (!finalUrl.length()) {
    Serial.println("Could not resolve stream URL");
    lcd.setCursor(0, 3);
    lcd.print("URL Error!           ");
    delay(2000);
    
    // Handle retry for URL resolution failure - stay on same station
    if (retryAttempt < maxRetries) {
      retryAttempt++;
      int retryDelay = 1000 * retryAttempt; // Exponential backoff: 1s, 2s, 3s
      Serial.print("Retrying same station in ");
      Serial.print(retryDelay);
      Serial.println("ms...");
      delay(retryDelay);
      startRadio();
      return;
    } else {
      // Max retries reached, reset counter and stay on same station
      Serial.println("Max URL retries reached, giving up on this station");
      retryAttempt = 0; // Reset counter
      lcd.setCursor(0, 3);
      lcd.print("URL failed!          ");
      return; // Don't auto-switch to next station
    }
  }
  
  Serial.print("Final Stream URL: ");
  Serial.println(finalUrl);
  
  // Convert HTTPS to HTTP for audio playback if needed
  String playUrl = makeHttpUrl(finalUrl);
  if (playUrl != finalUrl) {
    Serial.print("Playing via HTTP instead of HTTPS: ");
    Serial.println(playUrl);
  }

  // Show opening stream animation
  Serial.println("Opening stream connection...");
  for (int i = 0; i < 8; i++) {
    lcd.setCursor(0, 0);
    lcd.print(animChars[animIndex]);
    animIndex = (animIndex + 1) % 4;
    delay(60);
  }

  // Start new stream with pre-buffering
  if (file->open(playUrl.c_str())) {
    Serial.println("Stream opened, pre-buffering...");
       
    // Pre-buffer for better stability (wait for buffer to fill up)
    unsigned long bufferStart = millis();
    bool bufferReady = false;
    
    // First, give the stream time to start flowing
    delay(500); // Initial delay to let stream start
    
    // Animation characters for spinning effect
    char animChars[] = {'-', '\\', '|', '/'};
    int animIndex = 0;
    
    while (millis() - bufferStart < 5000) { // Wait up to 5 seconds for buffering
      // Update display to show buffering with spinning animation at position (0,0)
      lcd.setCursor(0, 0);
      lcd.print(animChars[animIndex]);
      animIndex = (animIndex + 1) % 4; // Cycle through animation characters

      // Keep the stream processing
      if (file->isOpen()) {
        buff->loop();
      }
      
      uint32_t currentLevel = buff->getFillLevel();
      if (currentLevel > 4096) { // Reduced threshold to 4KB for more reliability
        bufferReady = true;
        break;
      }
    
      delay(100); // Slightly longer delay to make animation visible
    }
    
    uint32_t finalLevel = buff->getFillLevel();
    Serial.print("Buffer level: ");
    Serial.print(finalLevel);
    Serial.println(" bytes");
    
    if (bufferReady || finalLevel > 1024) { // More lenient check - try if we have at least 1KB
      if (mp3->begin(buff, out)) {
        radioPlaying = true;
        retryAttempt = 0; // Reset retry counter on successful connection
        Serial.print("Started with buffer: ");
        Serial.print(finalLevel);
        Serial.print(" bytes - ");
        Serial.println(stationNames[currentStation]);
      } else {
        Serial.println("Failed to start MP3 decoder");
        file->close();
        
        // Handle retry for MP3 decoder failure - stay on same station
        if (retryAttempt < maxRetries) {
          retryAttempt++;
          int retryDelay = 1000 * retryAttempt; // Exponential backoff
          Serial.print("Retrying MP3 decoder on same station in ");
          Serial.print(retryDelay);
          Serial.println("ms...");
          delay(retryDelay);
          startRadio();
          return;
        } else {
          // Max retries reached for this station, reset counter and stay
          Serial.println("Max MP3 decoder retries reached, giving up on this station");
          retryAttempt = 0;
          lcd.setCursor(0, 3);
          lcd.print("MP3 fehlgeschlagen!  ");
          return; // Don't auto-switch to next station
        }
      }
    } else {
      Serial.print("Buffer failed to fill adequately (");
      Serial.print(finalLevel);
      Serial.println(" bytes) - handling retry");
      file->close();
      
      // Handle retry for buffer failure - stay on same station
      if (retryAttempt < maxRetries) {
        retryAttempt++;
        int retryDelay = 1000 * retryAttempt; // Exponential backoff
        lcd.clear();
        lcd.setCursor(0, 0);
        lcd.print("Verbindung fehlgeschlagen!");
        lcd.setCursor(0, 1);
        lcd.print("Versuch ");
        lcd.print(retryAttempt);
        lcd.print("/");
        lcd.print(maxRetries);
        lcd.print("...");
        Serial.print("Retrying buffer on same station in ");
        Serial.print(retryDelay);
        Serial.println("ms...");
        delay(retryDelay);
        startRadio();
        return;
      } else {
        // Max retries reached, reset counter and stay on same station
        Serial.println("Max buffer retries reached, giving up on this station");
        retryAttempt = 0;
        lcd.clear();
        lcd.setCursor(0, 0);
        lcd.print("Verbindung fehlgeschlagen!");
        delay(2000);
        updateDisplay(); // Show station info again
        return; // Don't auto-switch to next station
      }
    }
  } else {
    Serial.println("Failed to open stream");
    
    // Handle retry for stream opening failure - stay on same station
    if (retryAttempt < maxRetries) {
      retryAttempt++;
      int retryDelay = 1000 * retryAttempt; // Exponential backoff: 1s, 2s, 3s
      Serial.print("Retrying stream on same station in ");
      Serial.print(retryDelay);
      Serial.println("ms...");
      
      lcd.setCursor(0, 3);
      lcd.print("Versuch ");
      lcd.print(retryAttempt);
      lcd.print("/");
      lcd.print(maxRetries);
      lcd.print(" ");
      
      delay(retryDelay);
      startRadio();
      return;
    } else {
      // Max retries reached, reset counter and stay on same station
      Serial.println("Max stream retries reached, giving up on this station");
      retryAttempt = 0; // Reset counter
      lcd.setCursor(0, 3);
      lcd.print("Fehler mit Sender!  ");
      delay(2000);
      updateDisplay(); // Show station info again
      return; // Don't auto-switch to next station
    }
  }
  
  // Small delay to let buffering animation be visible before updating display
  delay(200);
  updateDisplay();
}

void stopRadio() {
  if (mp3 && mp3->isRunning()) {
    mp3->stop();
  }
  radioPlaying = false;
  Serial.println("Radio stopped");
  updateDisplay();
  
  // Turn off LCD backlight when audio is stopped
  lcd.noBacklight();
  backlightOn = false;
  
  // Enter deep sleep if enabled
  if (deepSleepEnabled) {
    enterDeepSleep();
  }
}

void enterDeepSleep() {
  Serial.println("Preparing for deep sleep...");
  
  // Wait a moment for the message to be visible
  delay(sleepDelay);
  
  // Clear the LCD to save power
  lcd.clear();
  lcd.noBacklight();
  
  // Configure wake-up source: joystick switch on GPIO32
  // The switch is normally HIGH (pull-up), goes LOW when pressed
  esp_sleep_enable_ext0_wakeup(GPIO_NUM_32, 0); // Wake on LOW signal
  
  // Disconnect WiFi to save power during sleep
  WiFi.disconnect();
  WiFi.mode(WIFI_OFF);
  
  // Stop any running tasks
  if (audioTaskHandle != NULL) {
    vTaskDelete(audioTaskHandle);
    audioTaskHandle = NULL;
  }
  
  Serial.println("Entering deep sleep...");
  Serial.flush(); // Make sure all serial data is sent before sleeping
  
  // Enter deep sleep
  esp_deep_sleep_start();
}

void changeStation(int direction) {
  // Stop current playback properly before changing stations
  bool wasPlaying = radioPlaying;
  if (wasPlaying && mp3 && mp3->isRunning()) {
    mp3->stop();
    radioPlaying = false;
    // Give the audio system time to clean up
    delay(200);
  }
  
  // Reset retry counter when manually changing stations
  retryAttempt = 0;
  
  if (direction > 0) {
    currentStation = (currentStation + 1) % numStations;
  } else {
    currentStation = (currentStation - 1 + numStations) % numStations;
  }
  
  Serial.print("Changed to station ");
  Serial.print(currentStation + 1);
  Serial.print(": ");
  Serial.println(stationNames[currentStation]);

  updateDisplay();
  
  // Clear old metadata from lines 3 and 4 when switching stations
  lcd.setCursor(0, 2);
  lcd.print("                    "); // Clear line 3 (20 spaces)
  lcd.setCursor(0, 3);
  lcd.print("                    "); // Clear line 4 (20 spaces)

  if (wasPlaying) {
    // Set playing state before starting radio to show correct symbol during buffering
    radioPlaying = true;
    updateDisplay(); // Update display with play symbol before buffering starts
    
    // Give additional time for cleanup before restarting
    delay(100);
    startRadio(); // Restart with new station
  }
}

void setup() {
  // Initialize serial communication for debugging
  Serial.begin(115200);
  
  // Check wake-up reason
  esp_sleep_wakeup_cause_t wakeup_reason = esp_sleep_get_wakeup_cause();
  
  switch(wakeup_reason) {
    case ESP_SLEEP_WAKEUP_EXT0:
      Serial.println("Wakeup caused by external signal using RTC_IO (joystick switch)");
      break;
    case ESP_SLEEP_WAKEUP_UNDEFINED:
    default:
      Serial.println("Starting ESP32 Web Radio...");
      break;
  }
  
  chipID = ESP32Helpers::getChipId();
  Serial.print("ESP32 Chip ID: ");
  Serial.println(chipID);

  // Initialize joystick pins
  pinMode(SW_PIN, INPUT_PULLUP);
  
  // Configure ADC for better analog reading
  analogReadResolution(12);
  // Configure attenuation for joystick pins specifically
  analogSetPinAttenuation(VRX_PIN, ADC_11db);
  analogSetPinAttenuation(VRY_PIN, ADC_11db);
  
  // Initialize the LCD
  lcd.init();
  lcd.backlight();
  backlightOn = true; // Track backlight state
  lcd.clear();
  
  // Show startup or wake-up message
  lcd.setCursor(0, 0);
  lcd.print("ESP32 Web Radio");
  lcd.setCursor(0, 1);
  
  if (wakeup_reason == ESP_SLEEP_WAKEUP_EXT0) {
    Serial.println("Device woken from deep sleep");
  } else {
    lcd.print("Bitte warten...");
  }
  delay(2000);
  
  // Connect to WiFi
  connectToWiFi();
  
  // Initialize I2S audio with better configuration (based on working code)
  out = new AudioOutputI2S(0, 1);     // Use internal DAC like working code
  out->SetOutputModeMono(true);       // Force mono output for stability
  out->SetGain(2.0f);                 // Higher gain like working code (increased from 1.0)
  out->RegisterStatusCB(statusCB, (void*)"OUT");
  
  file = new AudioFileSourceICYStream();
  file->useHTTP10();                  // Force HTTP/1.0 like working code
  file->SetReconnect(5, 1500);        // Auto-reconnect: 5 retries, 1.5s delay
  file->RegisterStatusCB(statusCB, (void*)"FILE");
  file->RegisterMetadataCB(metadataCB, (void*)"FILE");
  
  buff = new AudioFileSourceBuffer(file, 32768); // 32KB buffer for better stability
  mp3 = new AudioGeneratorMP3();
  mp3->RegisterStatusCB(statusCB, (void*)"MP3");
  mp3->RegisterMetadataCB(metadataCB, (void*)"MP3");
  
  // Create metadata queue for communication between cores
  metadataQueue = xQueueCreate(5, sizeof(MetadataMessage));
  
  // Create audio task on Core 1 (dedicated for audio processing)
  xTaskCreatePinnedToCore(
    audioTask,          // Task function
    "AudioTask",        // Task name
    4096,              // Stack size (4KB)
    NULL,              // Parameters
    2,                 // Priority (high priority)
    &audioTaskHandle,  // Task handle
    1                  // Core 1 (Core 0 is for main loop/UI)
  );
    
  updateDisplay();
  
  // Check if joystick switch is held during startup to disable deep sleep
  if (digitalRead(SW_PIN) == LOW) {
    deepSleepEnabled = false;
    Serial.println("Deep sleep disabled (switch held during startup)");
    lcd.setCursor(0, 3);
    delay(2000);
    updateDisplay();
  }
  
  // If woken from deep sleep, automatically start playing the radio
  if (wakeup_reason == ESP_SLEEP_WAKEUP_EXT0) {
    Serial.println("Auto-starting radio after wake-up");
    delay(1000); // Give time for everything to initialize
    startRadio();
  }
}

void loop() {
  otaInProgress = AzureOTAUpdater::CheckUpdateStatus();

   if (otaInProgress == 1) {
    lcd.setCursor(0, 1);
    lcd.print("Update läuft...");
   }
  // Handle metadata updates from queue (Core 0 - UI operations)
  MetadataMessage msg;
  if (metadataQueue != NULL && xQueueReceive(metadataQueue, &msg, 0) == pdTRUE) {
    // Update LCD with metadata on Core 0 - lines 3 and 4 (won't interrupt audio on Core 1)
    
    // Clear line 3 and display first line
    lcd.setCursor(0, 2);
    lcd.print("                    "); // Clear line 3 (20 spaces)
    lcd.setCursor(0, 2);
    lcd.print(msg.line1);
    
    // Clear line 4 and display second line (if any)
    lcd.setCursor(0, 3);
    lcd.print("                    "); // Clear line 4 (20 spaces)
    lcd.setCursor(0, 3);
    if (strlen(msg.line2) > 0) {
      lcd.print(msg.line2);
    }
  }
  
  // Update display if radio state changed (handled on Core 0)
  static bool lastRadioPlaying = radioPlaying;
  if (lastRadioPlaying != radioPlaying) {
    lastRadioPlaying = radioPlaying;
    updateDisplay();
  }
  
  // Read the switch state
  bool currentReading = digitalRead(SW_PIN);
  
  // Check if switch reading has changed (reset debounce timer)
  if (currentReading != switchReading) {
    lastDebounceTime = millis();
    switchReading = currentReading;
  }
  
  // If reading has been stable for debounce delay, update the stable state
  if ((millis() - lastDebounceTime) > debounceDelay) {
    // If stable state has changed, process the button action
    if (switchReading != lastStableState) {
      lastStableState = switchReading;
      
      // Only act on button PRESS (HIGH to LOW transition)
      if (switchReading == LOW) {
        switchPressed = true;
        Serial.println("Switch PRESS detected - Play/Stop toggle");
        
        // Toggle radio play/stop
        if (radioPlaying) {
          stopRadio();
        } else {
          startRadio();
        }
      } else {
        switchPressed = false;
      }
    }
  }
  
  // Read joystick analog values with delay to prevent too frequent updates
  if (millis() - lastJoystickRead >= joystickDelay) {
    lastJoystickRead = millis();
    
    // Read joystick analog values
    int vrxValue = analogRead(VRX_PIN);
    int vryValue = analogRead(VRY_PIN);
    
    // Check for joystick movements with more reliable thresholds
    // Center position is around 2048 (12-bit ADC), so use wider deadband
    if (vryValue > 3200) {  // Increased threshold for more reliable detection
      changeStation(1);
      Serial.println("Joystick Right - Next station");
      delay(100); // Small delay to prevent double-triggering
    }
    
    // Check for joystick DOWN (previous station)  
    else if (vryValue < 800) {  // Increased threshold for more reliable detection
      changeStation(-1);
      Serial.println("Joystick Left - Previous station");
      delay(100); // Small delay to prevent double-triggering
    }
    
  }
}