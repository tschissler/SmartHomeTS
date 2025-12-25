#ifndef MQTTCLIENTLIB_H
#define MQTTCLIENTLIB_H

#include <WiFi.h>
#include <MQTT.h>
#include <vector>

// MQTT Broker settings
const int mqtt_port = 32004;
// Define the maximum packet size for the MQTT client
#define MQTT_MAX_PACKET_SIZE 8192


class MQTTClientLib {
public:
    MQTTClientLib(const String& mqtt_broker, const String& clientId, WiFiClient& wifiClient, MQTTClientCallbackSimple callback);
    void connect(bool cleanSession);
    bool tryConnect(bool cleanSession, uint8_t maxAttempts = 3, uint32_t delayBetweenAttemptsMs = 1000);
    bool loop();
    bool publish(const String &topic, const String &payload, bool retained, int qos, bool printLogMessages = true);
    bool subscribe(const String& topic);
    bool subscribe(const std::vector<String>& topics);
    bool unsubscribe(const String &topic);
    bool connected();
    int lastError();
    int returnCode();

private:
    MQTTClient mqttClient;
    String clientId;
    String mqtt_broker;
};

#endif // MQTTCLIENTLIB_H