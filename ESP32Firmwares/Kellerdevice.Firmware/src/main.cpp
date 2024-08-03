#include <Arduino.h>
#include <WiFi.h>
#include <PubSubClient.h>
#include <time.h>
#include <ESP32httpUpdate.h>

const char* appName = "KellerDevice";
const char* version = "0.0.2";

// WiFi credentials are read from environment variables and used during compile-time (see platformio.ini)
// Set WIFI_SSID and WIFI_PASSWORD as environment variables on your dev-system
const char* ssid = WIFI_SSID;
const char* password = WIFI_PASSWORD;

// MQTT Broker settings
const char* mqtt_broker = "smarthomepi2";
const int mqtt_port = 32004;
const char* mqtt_update_topic = "OTAUpdateKellerdeviceTopic";

WiFiClient espClient;
PubSubClient mqttClient(espClient);

const int Red_LED_Pin = 13;
const int Green_LED_Pin = 12;
const int Blue_LED_Pin = 27;

const char* ca_cert PROGMEM = R"EOF(
-----BEGIN CERTIFICATE-----
MIIFrDCCBJSgAwIBAgIQCfluwpVVXyR0nq8eXc7UnTANBgkqhkiG9w0BAQwFADBh
MQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3
d3cuZGlnaWNlcnQuY29tMSAwHgYDVQQDExdEaWdpQ2VydCBHbG9iYWwgUm9vdCBH
MjAeFw0yMzA2MDgwMDAwMDBaFw0yNjA4MjUyMzU5NTlaMF0xCzAJBgNVBAYTAlVT
MR4wHAYDVQQKExVNaWNyb3NvZnQgQ29ycG9yYXRpb24xLjAsBgNVBAMTJU1pY3Jv
c29mdCBBenVyZSBSU0EgVExTIElzc3VpbmcgQ0EgMDQwggIiMA0GCSqGSIb3DQEB
AQUAA4ICDwAwggIKAoICAQDBeUy13eRZ/QC5bN7/IOGxodny7Xm2BFc88d3cca3y
HyyVx1Y60+afY6DAo/2Ls1uzAfbDfMzAVWJazPH4tckaItDv//htEbbNJnAGvZPB
4VqNviwDEmlAWT/MTAmzXfTgWXuUNgRlzZbjoFaPm+t6iJ6HdvDpWQAJbsBUZCga
t257tM28JnAHUTWdiDBn+2z6EGh2DA6BCx04zHDKVSegLY8+5P80Lqze0d6i3T2J
J7rfxCmxUXfCGOv9iQIUZfhv4vCb8hsm/JdNUMiomJhSPa0bi3rda/swuJHCH//d
wz2AGzZRRGdj7Kna4t6ToxK17lAF3Q6Qp368C9cE6JLMj+3UbY3umWCPRA5/Dms4
/wl3GvDEw7HpyKsvRNPpjDZyiFzZGC2HZmGMsrZMT3hxmyQwmz1O3eGYdO5EIq1S
W/vT1yShZTSusqmICQo5gWWRZTwCENekSbVX9qRr77o0pjKtuBMZTGQTixwpT/rg
Ul7Mr4M2nqK55Kovy/kUN1znfPdW/Fj9iCuvPKwKFdyt2RVgxJDvgIF/bNoRkRxh
wVB6qRgs4EiTrNbRoZAHEFF5wRBf9gWn9HeoI66VtdMZvJRH+0/FDWB4/zwxS16n
nADJaVPXh6JHJFYs9p0wZmvct3GNdWrOLRAG2yzbfFZS8fJcX1PYxXXo4By16yGW
hQIDAQABo4IBYjCCAV4wEgYDVR0TAQH/BAgwBgEB/wIBADAdBgNVHQ4EFgQUO3DR
U+l2JZ1gqMpmD8abrm9UFmowHwYDVR0jBBgwFoAUTiJUIBiV5uNu5g/6+rkS7QYX
jzkwDgYDVR0PAQH/BAQDAgGGMB0GA1UdJQQWMBQGCCsGAQUFBwMBBggrBgEFBQcD
AjB2BggrBgEFBQcBAQRqMGgwJAYIKwYBBQUHMAGGGGh0dHA6Ly9vY3NwLmRpZ2lj
ZXJ0LmNvbTBABggrBgEFBQcwAoY0aHR0cDovL2NhY2VydHMuZGlnaWNlcnQuY29t
L0RpZ2lDZXJ0R2xvYmFsUm9vdEcyLmNydDBCBgNVHR8EOzA5MDegNaAzhjFodHRw
Oi8vY3JsMy5kaWdpY2VydC5jb20vRGlnaUNlcnRHbG9iYWxSb290RzIuY3JsMB0G
A1UdIAQWMBQwCAYGZ4EMAQIBMAgGBmeBDAECAjANBgkqhkiG9w0BAQwFAAOCAQEA
o9sJvBNLQSJ1e7VaG3cSZHBz6zjS70A1gVO1pqsmX34BWDPz1TAlOyJiLlA+eUF4
B2OWHd3F//dJJ/3TaCFunjBhZudv3busl7flz42K/BG/eOdlg0kiUf07PCYY5/FK
YTIch51j1moFlBqbglwkdNIVae2tOu0OdX2JiA+bprYcGxa7eayLetvPiA77ynTc
UNMKOqYB41FZHOXe5IXDI5t2RsDM9dMEZv4+cOb9G9qXcgDar1AzPHEt/39335zC
HofQ0QuItCDCDzahWZci9Nn9hb/SvAtPWHZLkLBG6I0iwGxvMwcTTc9Jnb4Flysr
mQlwKsS2MphOoI23Qq3cSA==
-----END CERTIFICATE-----
)EOF";

void setupTime() {
 // Set timezone to Central European Time (CET) with daylight saving time (CEST)
  // CET is UTC+1, CEST is UTC+2
  // The timezone string format is: "TZ=std offset dst offset, start[/time], end[/time]"
  // For CET/CEST: "CET-1CEST,M3.5.0/2,M10.5.0/3"
  // This means:
  // - Standard time is CET (UTC+1)
  // - Daylight saving time is CEST (UTC+2)
  // - DST starts on the last Sunday of March at 2:00 AM
  // - DST ends on the last Sunday of October at 3:00 AM
  configTzTime("CET-1CEST,M3.5.0/2,M10.5.0/3", "pool.ntp.org", "time.nist.gov");

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

void updateFirmwareFromUrl(const String &firmwareUrl) {
    Serial.print("Downloading new firmware from: ");
    Serial.println(firmwareUrl);

    digitalWrite(Red_LED_Pin, HIGH);
    digitalWrite(Blue_LED_Pin, HIGH);
    
    t_httpUpdate_return ret = ESPhttpUpdate.update(firmwareUrl);
    Serial.println("Firmware update process completed.");
    Serial.print("HTTP Update result: ");
    Serial.println(ret);
    
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

        default:
            Serial.println("Unknown response");
            Serial.println(ret);
            break;
    }
    Serial.println();

    digitalWrite(Red_LED_Pin, LOW);
    digitalWrite(Blue_LED_Pin, LOW);
}

void connectToMQTT() {
    mqttClient.setServer(mqtt_broker, mqtt_port);
    while (!mqttClient.connected()) {
        if (mqttClient.connect("ESP32KellerdeviceClient")) {
            mqttClient.subscribe(mqtt_update_topic);
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

    if (String(topic) == mqtt_update_topic) {
      // Trigger OTA Update
      Serial.println("OTA Update Triggered");
      String firmwareUrl = messageTemp;
      updateFirmwareFromUrl(firmwareUrl);
    }
}

void setup() {
  Serial.begin(9600);
  Serial.println(String(appName) + " " + String(version));  
  
  // Set LED pins as output
  pinMode(Red_LED_Pin, OUTPUT);
  pinMode(Green_LED_Pin, OUTPUT);
  pinMode(Blue_LED_Pin, OUTPUT);

  // Turn off all LEDs, turn on blue LED to indicate connecting to WiFi
  digitalWrite(Red_LED_Pin, LOW);
  digitalWrite(Green_LED_Pin, LOW);
  digitalWrite(Blue_LED_Pin, HIGH);

  // Connect to WiFi
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(1000);
    Serial.println("Connecting to WiFi " + String(ssid) + " with password " + String(password) + "...");
  }
  Serial.println("Connected to WiFi");

  setupTime();
  
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
  
  digitalWrite(Blue_LED_Pin, LOW);
  digitalWrite(Green_LED_Pin, HIGH);

}

void loop() {
  if (!mqttClient.connected()) {
    connectToMQTT();
  }
  mqttClient.loop();
  digitalWrite(Green_LED_Pin, HIGH);
  delay(100);
  digitalWrite(Green_LED_Pin, LOW);
  delay(2000);
}
