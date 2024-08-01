#include <Arduino.h>
#include <FastLED.h>
#include <WiFi.h>
#include <WebServer.h>
#include <PubSubClient.h>
#include <time.h>
#include <ESP32httpUpdate.h>

const char* version = "0.0.8";

#define DATA_PIN_1 13
#define DATA_PIN_2 12
#define NUM_LEDS 256
CRGB leds[NUM_LEDS];

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_SSID and WIFI_PASSWORD as environment variables on your dev-system
const char* ssid = WIFI_SSID;
const char* password = WIFI_PASSWORD;

const char* ca_cert = \
"-----BEGIN CERTIFICATE-----\n" \
"MIINzDCCC7SgAwIBAgITSwAzAcVJuknedMmbwAAAADMBxTANBgkqhkiG9w0BAQsF\n" \
"ADBPMQswCQYDVQQGEwJVUzEeMBwGA1UEChMVTWljcm9zb2Z0IENvcnBvcmF0aW9u\n" \
"MSAwHgYDVQQDExdNaWNyb3NvZnQgUlNBIFRMUyBDQSAwMTAeFw0yMzA5MjcwNTM2\n" \
"MzNaFw0yNDA5MjcwNTM2MzNaMCIxIDAeBgNVBAMMFyouYmxvYi5jb3JlLndpbmRv\n" \
"d3MubmV0MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA5dlR7RZ/VGl8\n" \
"nryt/GoN9CrXUMkxjx+Oy+NFlF08ParqxbERAuwdLscvyL6KdQAK2kwXf9KToXg0\n" \
"ROqxx+mfm00LauVTxZhQzYdmhnCPlnFEVYu3b7KnVz+9AFHxu+ZDUt0XY1dlCLew\n" \
"1AX3oJgaGujvDa25PjgNZgDdlFOtrupnLDhqCEBqy3nvbcf3MW/calbxruJLU1Pp\n" \
"+7QoFuFEsu82WyniKYyrkmbMH96/byHMC5FtiNq/pYrEsiU8MChQsDOU19t1m64/\n" \
"TEMfQEp8SFYmcRXc5syVk33WTAL6VSgd2w6EduH91xvn1Y5ptjNc4Y1chEiKm2XB\n" \
"W6TplibKaQIDAQABo4IJzDCCCcgwggF/BgorBgEEAdZ5AgQCBIIBbwSCAWsBaQB3\n" \
"AHb/iD8KtvuVUcJhzPWHujS0pM27KdxoQgqf5mdMWjp0AAABitUszmoAAAQDAEgw\n" \
"RgIhAPatkE8zbE9cITnGdv/UlprO8wqaeSkYwwlKje0EL3JEAiEAoL+Xjyavs6Rf\n" \
"Tmz7awRVQx16FRqmMXljNK+nCKS/K1gAdQDatr9rP7W2Ip+bwrtca+hwkXFsu1GE\n" \
"hTS9pD0wSNf7qwAAAYrVLM5VAAAEAwBGMEQCIAbSvs8DW7d/P0a7b9Dzob+xJaqa\n" \
"mgldvGHdeIqydJIFAiArW/p6atVwI2aoPJ/GR80xBkeFFj2avz15QfAO4mIfBAB3\n" \
"AO7N0GTV2xrOxVy3nbTNE6Iyh0Z8vOzew1FIWUZxH7WbAAABitUszhwAAAQDAEgw\n" \
"RgIhAMPpkk6PRpZTSK+odMfkuVGT5Y6XP2k5Eoe2SdAetCgqAiEA8qxIlcX1UO4M\n" \
"DlbW6L1/Zh+NV9ZU7GwCSyexDTdbTCIwgYcGCCsGAQUFBwEBBHsweTAiBggrBgEF\n" \
"BQcwAYYWaHR0cDovL29jc3AubXNvY3NwLmNvbTBTBggrBgEFBQcwAoZHaHR0cDov\n" \
"L3d3dy5taWNyb3NvZnQuY29tL3BraS9tc2NvcnAvTWljcm9zb2Z0JTIwUlNBJTIw\n" \
"VExTJTIwQ0ElMjAwMS5jcnQwHQYDVR0OBBYEFC1pACWaWIp1/sY+M+nbdfW51OXC\n" \
"MA4GA1UdDwEB/wQEAwIFoDCCBjwGA1UdEQSCBjMwggYvghcqLmJsb2IuY29yZS53\n" \
"aW5kb3dzLm5ldIInKi5mcmEyMXByZHN0cjAzYS5zdG9yZS5jb3JlLndpbmRvd3Mu\n" \
"bmV0ghgqLmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCGyouejEuYmxvYi5zdG9yYWdl\n" \
"LmF6dXJlLm5ldIIbKi56Mi5ibG9iLnN0b3JhZ2UuYXp1cmUubmV0ghsqLnozLmJs\n" \
"b2Iuc3RvcmFnZS5henVyZS5uZXSCGyouejQuYmxvYi5zdG9yYWdlLmF6dXJlLm5l\n" \
"dIIbKi56NS5ibG9iLnN0b3JhZ2UuYXp1cmUubmV0ghsqLno2LmJsb2Iuc3RvcmFn\n" \
"ZS5henVyZS5uZXSCGyouejcuYmxvYi5zdG9yYWdlLmF6dXJlLm5ldIIbKi56OC5i\n" \
"bG9iLnN0b3JhZ2UuYXp1cmUubmV0ghsqLno5LmJsb2Iuc3RvcmFnZS5henVyZS5u\n" \
"ZXSCHCouejEwLmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejExLmJsb2Iuc3Rv\n" \
"cmFnZS5henVyZS5uZXSCHCouejEyLmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCou\n" \
"ejEzLmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejE0LmJsb2Iuc3RvcmFnZS5h\n" \
"enVyZS5uZXSCHCouejE1LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejE2LmJs\n" \
"b2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejE3LmJsb2Iuc3RvcmFnZS5henVyZS5u\n" \
"ZXSCHCouejE4LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejE5LmJsb2Iuc3Rv\n" \
"cmFnZS5henVyZS5uZXSCHCouejIwLmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCou\n" \
"ejIxLmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejIyLmJsb2Iuc3RvcmFnZS5h\n" \
"enVyZS5uZXSCHCouejIzLmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejI0LmJs\n" \
"b2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejI1LmJsb2Iuc3RvcmFnZS5henVyZS5u\n" \
"ZXSCHCouejI2LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejI3LmJsb2Iuc3Rv\n" \
"cmFnZS5henVyZS5uZXSCHCouejI4LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCou\n" \
"ejI5LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejMwLmJsb2Iuc3RvcmFnZS5h\n" \
"enVyZS5uZXSCHCouejMxLmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejMyLmJs\n" \
"b2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejMzLmJsb2Iuc3RvcmFnZS5henVyZS5u\n" \
"ZXSCHCouejM0LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejM1LmJsb2Iuc3Rv\n" \
"cmFnZS5henVyZS5uZXSCHCouejM2LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCou\n" \
"ejM3LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejM4LmJsb2Iuc3RvcmFnZS5h\n" \
"enVyZS5uZXSCHCouejM5LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejQwLmJs\n" \
"b2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejQxLmJsb2Iuc3RvcmFnZS5henVyZS5u\n" \
"ZXSCHCouejQyLmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejQzLmJsb2Iuc3Rv\n" \
"cmFnZS5henVyZS5uZXSCHCouejQ0LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCou\n" \
"ejQ1LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejQ2LmJsb2Iuc3RvcmFnZS5h\n" \
"enVyZS5uZXSCHCouejQ3LmJsb2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejQ4LmJs\n" \
"b2Iuc3RvcmFnZS5henVyZS5uZXSCHCouejQ5LmJsb2Iuc3RvcmFnZS5henVyZS5u\n" \
"ZXSCHCouejUwLmJsb2Iuc3RvcmFnZS5henVyZS5uZXQwgbAGA1UdHwSBqDCBpTCB\n" \
"oqCBn6CBnIZNaHR0cDovL21zY3JsLm1pY3Jvc29mdC5jb20vcGtpL21zY29ycC9j\n" \
"cmwvTWljcm9zb2Z0JTIwUlNBJTIwVExTJTIwQ0ElMjAwMS5jcmyGS2h0dHA6Ly9j\n" \
"cmwubWljcm9zb2Z0LmNvbS9wa2kvbXNjb3JwL2NybC9NaWNyb3NvZnQlMjBSU0El\n" \
"MjBUTFMlMjBDQSUyMDAxLmNybDBXBgNVHSAEUDBOMAgGBmeBDAECATBCBgkrBgEE\n" \
"AYI3KgEwNTAzBggrBgEFBQcCARYnaHR0cDovL3d3dy5taWNyb3NvZnQuY29tL3Br\n" \
"aS9tc2NvcnAvY3BzMB8GA1UdIwQYMBaAFLV2DDARzseSQk1Mx1wsyKkM6AtkMB0G\n" \
"A1UdJQQWMBQGCCsGAQUFBwMBBggrBgEFBQcDAjANBgkqhkiG9w0BAQsFAAOCAgEA\n" \
"YFonZHWxDtdC8uO4LZ+kr6AKR1GnR/YYXQq+PN1t+XYSvYhJzsW9SVcY3z2XJ0VT\n" \
"2H46rfJzPCcC/RSEpnm6ewEVYQ05oFkRuZYpt2K1c86k3RvnptIQJQEm5kQLDNlw\n" \
"LMLs7N8B7uP3NuthaMNseOuD9PBxO1DpKGGhM86Co1gGb08pEeUWP9TYrJdCMVX2\n" \
"dFkhUaP9UKaBnNMZ0BFXTVUVpel8GrS7Q5U/8EMg2BFSu0KzqkvX8/V+3omTan2c\n" \
"elx9R4z9wDqF1NF5HlNMm4Ghk2tkyA8CkYQguVK8FpaXKgK2546oTkPQ5Y9o2s+x\n" \
"jwtbuqmNvbShG5sReAuhzwnZrmHs58T4ZBe2GkYk8ZJzyqUojPBnaoiQ2lVQ+TUm\n" \
"kq2Kd1GjhVrHcJEK/I7FL1+jiGeHY+lgWnvIj4Z3WuO930VI1oNumOogfxYSS9ur\n" \
"FFh8tfSdELTpNZTqrmGE2XZCLvs2N54ihr7wr9KWjltmFwkkw+N1brW7L/ziqAVp\n" \
"MoMzupo8I7EvIwb81bZhNDDoCnP0cg/gSujiHWkVvgQoNEO3vFyai43dLgvNCXXX\n" \
"0+LcLSHNBYuaxQmfoMlKDAX52ReQbgDBfdaExkJRxd+fiu8YWk6vPt/7/7G16HBC\n" \
"LSJ9vjdFTTp7zjVFadG+7/s8ByfMrmWQkU+VqwcJ3mg=\n" \
"-----END CERTIFICATE-----\n";

// MQTT Broker settings
const char* mqtt_broker = "smarthomepi2";
const int mqtt_port = 32004;
const char* mqtt_topic = "OTAUpdateLEDStripeTopic";

WebServer server(80);
WiFiClient espClient;
PubSubClient mqttClient(espClient);

void setupTime() {
  // Set timezone (e.g., UTC +1:00)
  // Change according to your timezone
  // For UTC -5:00, use -5 * 3600, and so on
  configTime(1 * 3600, 0, "pool.ntp.org", "time.nist.gov");

  // Wait until time is set
  time_t now = time(nullptr);
  while (now < 8 * 3600 * 2) {
    delay(500);
    now = time(nullptr);
  }

  struct tm timeinfo;
  if (!getLocalTime(&timeinfo)) {
    Serial.println("Failed to obtain time");
    return;
  }
  Serial.println(&timeinfo, "%A, %B %d %Y %H:%M:%S");
}

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
  for (int i = 0; i < NUM_LEDS; i++) {
    leds[i] = CRGB(10, 80, 10);
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
  Serial.print("LEDStripe ");
  Serial.println(version);
  
  // Connect to WiFi
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(1000);
    Serial.println("Connecting to WiFi " + String(ssid) + " with password " + String(password) + "...");
  }
  Serial.println("Connected to WiFi");

  setupTime();

  initLED();
  delay(500);
  clear();
  delay(500);
  initLED();
  delay(500);
  clear();

  WiFiClientSecure client;
  client.setCACert(ca_cert);

  if (!client.connect("iotstoragem1.blob.core.windows.net", 443)) {
    Serial.println("Connection failed!");
  }
  
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
