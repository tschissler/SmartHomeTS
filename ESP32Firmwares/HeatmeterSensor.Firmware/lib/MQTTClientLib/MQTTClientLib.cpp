#include "MQTTClientLib.h"

MQTTClientLib::MQTTClientLib(const String& mqtt_broker, const String& clientId, WiFiClient& wifiClient, MQTTClientCallbackSimple callback) 
    : mqttClient(MQTTClient(MQTT_MAX_PACKET_SIZE)), clientId(clientId), mqtt_broker(mqtt_broker) {
    mqttClient.begin(mqtt_broker.c_str(), mqtt_port, wifiClient);
    mqttClient.onMessage(callback);
    mqttClient.setOptions(60, false, 1000);
}

void MQTTClientLib::connect(std::vector<String> subscriptionTopics) {
    while (!mqttClient.connected()) {
        Serial.print("Connecting to MQTT Broker ");
        Serial.print(mqtt_broker);
        Serial.print(" with ClientId ");
        Serial.println(clientId);
        if (mqttClient.connect(clientId.c_str())) {
            Serial.println("Connected to MQTT Broker");
        } else {
        Serial.print("Failed to connect to MQTT Broker ");
        Serial.print(mqtt_broker);
        Serial.print(" with Last Error: ");
        Serial.println(mqttClient.lastError());
        delay(1000);
        }
    }

     // Subscribe to the list of topics
    for (const String& topic : subscriptionTopics) {
        bool subscribeSuccess = mqttClient.subscribe(topic.c_str());
        if (subscribeSuccess) {
            Serial.print("Subscribed to topic: ");
            Serial.println(topic);
        } else {
            Serial.print("Failed to subscribe to topic: ");
            Serial.println(topic);
            Serial.print("Last Error: ");
            Serial.println(mqttClient.lastError());
        }
    }
    Serial.println("Connected to MQTT Broker and subscribed to topics");
}

bool MQTTClientLib::loop() {
    return mqttClient.loop();
}

bool MQTTClientLib::publish(const String& topic, const String& payload, bool retained, int qos) {
    bool mqttSuccess = mqttClient.publish(topic.c_str(), payload.c_str(), retained, qos);
    //Serial.println(mqttSuccess?"Published new values to MQTT Broker on topic " + topic:"Publishing to MQTT Broker failed");
    //Serial.println(" -> Connected:" + String(mqttClient.connected()) + " -> LastError:"  + String(mqttClient.lastError())  + " -> ReturnCode:" + String(mqttClient.returnCode()));
    return mqttSuccess;
}

bool MQTTClientLib::subscribe(const String& topic) {
    return mqttClient.subscribe(topic.c_str());
}

int MQTTClientLib::lastError() {
    return mqttClient.lastError();
}