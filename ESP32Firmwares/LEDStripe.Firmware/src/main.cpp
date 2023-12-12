#include <Arduino.h>
#include <FastLED.h>
#include <WiFi.h>
#include <WebServer.h>
#include <PubSubClient.h>
// #include <ArduinoOTA.h>
//#include <HTTPClient.h>
//#include <Update.h>
#include <ESP32httpUpdate.h>

#define DATA_PIN_1 13
#define DATA_PIN_2 12
#define NUM_LEDS 256
CRGB leds[NUM_LEDS];

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_SSID and WIFI_PASSWORD as environment variables on your dev-system
const char* ssid = WIFI_SSID;
const char* password = WIFI_PASSWORD;

// MQTT Broker settings
const char* mqtt_broker = "smarthomepi2";
const int mqtt_port = 32004;
const char* mqtt_topic = "OTAUpdateLEDStripeTopic";

WebServer server(80);
WiFiClient espClient;
PubSubClient mqttClient(espClient);

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

void updateFirmwareFromUrl(const String &firmwareUrl) {
    Serial.print("Downloading new firmware from: ");
    Serial.println(firmwareUrl);
    
    t_httpUpdate_return ret = ESPhttpUpdate.update(firmwareUrl);
    
    switch(ret) {
        case HTTP_UPDATE_FAILED:
            Serial.printf("HTTP_UPDATE_FAILD Error (%d): %s", ESPhttpUpdate.getLastError(), ESPhttpUpdate.getLastErrorString().c_str());
            break;

        case HTTP_UPDATE_NO_UPDATES:
            Serial.println("HTTP_UPDATE_NO_UPDATES");
            break;

        case HTTP_UPDATE_OK:
            Serial.println("HTTP_UPDATE_OK");
                Serial.println("Update done");
            break;
    }
    Serial.println();
}

void connectToMQTT() {
    mqttClient.setServer(mqtt_broker, mqtt_port);
    while (!mqttClient.connected()) {
        if (mqttClient.connect("ESP32LEDStripeClient")) {
            mqttClient.subscribe(mqtt_topic);
            Serial.println("Connected to MQTT Broker");
        } else {
            Serial.print("Failed to connect to MQTT Broker: ");
            Serial.println(mqtt_broker);
            delay(5000);
        }
    }
}

void mqttCallback(char* topic, byte* message, unsigned int length) {
    Serial.print("Message arrived on topic: ");
    Serial.print(topic);
    Serial.print(". Message: ");

    String messageTemp;

    for (int i = 0; i < length; i++) {
        messageTemp += (char)message[i];
    }

    Serial.println(messageTemp);

    if (String(topic) == mqtt_topic) {
          // Trigger OTA Update
          Serial.println("OTA Update Triggered");
          String firmwareUrl = messageTemp;
          updateFirmwareFromUrl(firmwareUrl);
    }
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

  // Set up MQTT
  mqttClient.setCallback(mqttCallback);
  connectToMQTT();

  // Define endpoint
  server.on("/setColor", HTTP_GET, setColor);

  // Start server
  server.begin();
}

void loop() {
  if (!mqttClient.connected()) {
    connectToMQTT();
  }
  mqttClient.loop();

  server.handleClient();
}
