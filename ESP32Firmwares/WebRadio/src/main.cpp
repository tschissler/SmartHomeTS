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
  "http://streams.80s80s.de/web/mp3-192/streams.80s80s.de/", // 80s80s 192k (WORKS GREAT!)
  "http://regiocast.streamabc.net/regc-radiobobclassicrock1594088-mp3-128-5621663",                 // Classic Rock Florida 128k
  "http://stream.radiorockrevolution.com:443/listen.mp3", // Rock Revolution 128k
  "http://184.75.223.134:8347/stream",                // Heavy Metal Radio 128k (WORKS!)
  "http://radio.spainmedia.es:8006/stream"            // Rock & Metal Radio 128k
};

const String stationNames[] = {
  "SWR4 Ulm",
  "80s80s Radio",
  "Radio BOB\nClassic Rock", 
  "Rock Revolution",
  "Heavy Metal",
  "Rock & Metal"
};

const int numStations = sizeof(stations) / sizeof(stations[0]);

// Radio variables
int currentStation = 0;
bool radioPlaying = false;
bool backlightOn = true;
unsigned long lastJoystickRead = 0;
unsigned long lastAudioCheck = 0;
unsigned long streamStartTime = 0;
const unsigned long STREAM_TIMEOUT = 10000; // 10 seconds timeout

// Robust switch debouncing variables
bool switchPressed = false;
bool lastStableState = HIGH;
bool switchReading = HIGH;
unsigned long lastDebounceTime = 0;
const unsigned long debounceDelay = 50;
const unsigned long joystickDelay = 200; // Reduced for better responsiveness during audio

void updateDisplay() {
  // Clear first two lines
  lcd.setCursor(0, 0);
  lcd.print("                    "); // Clear line 1
  lcd.setCursor(0, 1);
  lcd.print("                    "); // Clear line 2
  
  // Line 1+2: Station info with play status
  lcd.setCursor(0, 0);
  String stationName = stationNames[currentStation];
  int newlineIndex = stationName.indexOf('\n');
  if (newlineIndex != -1) {
    lcd.print(stationName.substring(0, newlineIndex));
    lcd.setCursor(0, 1);
    String line2 = stationName.substring(newlineIndex + 1);
    // Add play status to second line if there's space
    if (line2.length() <= 13) {
      lcd.print(line2);
      lcd.print(" ");
      if (!radioPlaying) {
        lcd.print("AUS");
      } else {
        lcd.print("ON");
      }
    } else {
      lcd.print(line2);
    }
  } else {
    // Single line station name - add play status if there's space
    if (stationName.length() <= 16) {
      lcd.print(stationName);
      lcd.print(" ");
      if (!radioPlaying) {
        lcd.print("AUS");
      } else {
        lcd.print("ON");
      }
    } else {
      lcd.print(stationName);
    }
  }
  // Note: Lines 3 and 4 are now reserved for metadata display
}

void connectToWiFi() {
  lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print("Verbinden...");
  
  WiFi.begin(ssid, password);
  
  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 20) {
    delay(500);
    Serial.print(".");
    lcd.setCursor(0, 1);
    for (int i = 0; i < (attempts % 16); i++) {
      lcd.print(".");
    }
    attempts++;
  }
  
  if (WiFi.status() == WL_CONNECTED) {
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print("Verbunden!");
    lcd.setCursor(0, 1);
    lcd.print(WiFi.localIP());
    delay(2000);
  } else {
    Serial.println("WiFi Verbindung fehlgeschlagen!");
    lcd.clear();
    lcd.setCursor(0, 0);
    lcd.print("WiFi Verbingung fehlgeschlagen!");
    delay(2000);
  }
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
  if (WiFi.status() != WL_CONNECTED) {
    Serial.println("WiFi not connected!");
    return;
  }
  
  // Stop any current playback
  if (mp3 && mp3->isRunning()) {
    mp3->stop();
  }
  
  Serial.print("Connecting to: ");
  Serial.println(stations[currentStation]);
  
  // Resolve redirects and playlists
  String finalUrl = resolveStreamUrl(stations[currentStation], 6, 7000);
  if (!finalUrl.length()) {
    Serial.println("Could not resolve stream URL");
    lcd.setCursor(0, 3);
    lcd.print("URL Error!           ");
    delay(2000);
    // Try next station
    currentStation = (currentStation + 1) % numStations;
    updateDisplay();
    delay(1000);
    startRadio();
    return;
  }
  
  Serial.print("Final Stream URL: ");
  Serial.println(finalUrl);
  
  // Convert HTTPS to HTTP for audio playback if needed
  String playUrl = makeHttpUrl(finalUrl);
  if (playUrl != finalUrl) {
    Serial.print("Playing via HTTP instead of HTTPS: ");
    Serial.println(playUrl);
  }

  // Start new stream with pre-buffering
  if (file->open(playUrl.c_str())) {
    Serial.println("Stream opened, pre-buffering...");
    
    // Update display to show buffering
    lcd.setCursor(0, 3);
    lcd.print("Puffern...           ");
    
    // Pre-buffer for better stability (wait for buffer to fill up)
    unsigned long bufferStart = millis();
    bool bufferReady = false;
    
    // First, give the stream time to start flowing
    delay(500); // Initial delay to let stream start
    
    while (millis() - bufferStart < 5000) { // Wait up to 5 seconds for buffering
      // Keep the stream processing
      if (file->isOpen()) {
        buff->loop();
      }
      
      uint32_t currentLevel = buff->getFillLevel();
      if (currentLevel > 4096) { // Reduced threshold to 4KB for more reliability
        bufferReady = true;
        break;
      }
    
      delay(50); // Smaller delay to keep responsive
    }
    
    uint32_t finalLevel = buff->getFillLevel();
    Serial.print("Buffer level: ");
    Serial.print(finalLevel);
    Serial.println(" bytes");
    
    if (bufferReady || finalLevel > 1024) { // More lenient check - try if we have at least 1KB
      if (mp3->begin(buff, out)) {
        radioPlaying = true;
        Serial.print("Started with buffer: ");
        Serial.print(finalLevel);
        Serial.print(" bytes - ");
        Serial.println(stationNames[currentStation]);
      } else {
        Serial.println("Failed to start MP3 decoder");
        file->close();
      }
    } else {
      Serial.print("Buffer failed to fill adequately (");
      Serial.print(finalLevel);
      Serial.println(" bytes) - trying next station");
      file->close();
      
      // Auto-try next station if buffer fails
      currentStation = (currentStation + 1) % numStations;
      lcd.clear();
      lcd.setCursor(0, 0);
      lcd.print("Buffer failed!");
      lcd.setCursor(0, 1);
      lcd.print("Trying next...");
      delay(1000);
      startRadio(); // Recursive call to try next station
      return;
    }
  } else {
    Serial.println("Failed to open stream");
  }
  
  updateDisplay();
}

void stopRadio() {
  if (mp3 && mp3->isRunning()) {
    mp3->stop();
  }
  radioPlaying = false;
  Serial.println("Radio stopped");
  updateDisplay();
}

void changeStation(int direction) {
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

  if (radioPlaying) {
    startRadio(); // Restart with new station
  }
}

void setup() {
  // Initialize serial communication for debugging
  Serial.begin(115200);
  Serial.println("Starting ESP32 Web Radio...");
  
  // Initialize joystick pins
  pinMode(SW_PIN, INPUT_PULLUP);
  
  // Configure ADC for better analog reading
  analogReadResolution(12);
  analogSetAttenuation(ADC_11db);
  
  // Initialize the LCD
  lcd.init();
  lcd.backlight();
  lcd.clear();
  
  // Show startup message
  lcd.setCursor(0, 0);
  lcd.print("ESP32 Web Radio");
  lcd.setCursor(0, 1);
  lcd.print("Bitte warten...");
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
}

void loop() {
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
    
    // Check for joystick UP (next station)
    if (vryValue > 3000) {
      changeStation(1);
      Serial.println("Joystick Right - Next station");
    }
    
    // Check for joystick DOWN (previous station)
    else if (vryValue < 1000) {
      changeStation(-1);
      Serial.println("Joystick Left - Previous station");
    }
    
  }
}