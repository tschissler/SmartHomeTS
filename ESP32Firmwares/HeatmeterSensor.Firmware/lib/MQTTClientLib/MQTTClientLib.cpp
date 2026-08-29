#include "MQTTClientLib.h"
#include <esp_system.h>

MQTTClientLib::MQTTClientLib(const String& mqtt_broker, int mqtt_port, const String& clientId, WiFiClient& wifiClient, MQTTClientCallbackSimple callback) 
    : mqttClient(MQTTClient(MQTT_MAX_PACKET_SIZE)), clientId(clientId), mqtt_broker(mqtt_broker) {
    mqttClient.begin(mqtt_broker.c_str(), mqtt_port, wifiClient);
    mqttClient.onMessage(callback);
    mqttClient.setOptions(60, false, 10000);
}

void MQTTClientLib::connect(bool cleanSession) {
    while (!mqttClient.connected()) {
        Serial.println("=== MQTT Connection Attempt ===");
        Serial.print("Clean Session: ");
        Serial.println(cleanSession ? "true" : "false");

        mqttClient.setCleanSession(cleanSession);
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

    Serial.println("Connected to MQTT Broker");
    connects++;

    // Restore every known subscription on the fresh connection
    resubscribeAll();
}

bool MQTTClientLib::connected() {
    return mqttClient.connected();
}

bool MQTTClientLib::loop() {
    bool connectionAlive = mqttClient.loop();

    if (connectionAlive) {
        // Pick up subscriptions the broker has not acknowledged yet, so a single lost
        // SUBACK does not leave the device deaf until the next reconnect
        retryPendingSubscriptions();
    }

    return connectionAlive;
}

bool MQTTClientLib::publish(const String& topic, const String& payload, bool retained, int qos, bool printLogMessages) {
    bool mqttSuccess = mqttClient.publish(topic.c_str(), payload.c_str(), retained, qos);
    if (printLogMessages) {
        Serial.println(mqttSuccess?"Published new values to MQTT Broker on topic " + topic:"Publishing to MQTT Broker failed");
        Serial.println(" -> Connected:" + String(mqttClient.connected()) + " -> LastError:"  + String(mqttClient.lastError())  + " -> ReturnCode:" + String(mqttClient.returnCode()));
    }
    return mqttSuccess;
}

bool MQTTClientLib::sendSubscribe(const String& topic) {
    bool subscribeSuccess = mqttClient.subscribe(topic.c_str());
    if (subscribeSuccess) {
        Serial.print("Subscribed to topic: ");
        Serial.println(topic);
    } else {
        Serial.print("#####################################################################Failed to subscribe to topic: ");
        Serial.println(topic);
        Serial.print("Last Error: ");
        Serial.println(mqttClient.lastError());
    }
    return subscribeSuccess;
}

void MQTTClientLib::rememberSubscription(const String& topic, bool acknowledged) {
    for (auto& subscription : subscriptions) {
        if (subscription.topic == topic) {
            subscription.acknowledged = acknowledged;
            return;
        }
    }
    subscriptions.push_back({topic, acknowledged});
}

void MQTTClientLib::resubscribeAll() {
    if (subscriptions.empty()) {
        return;
    }

    Serial.println("Restoring " + String(subscriptions.size()) + " MQTT subscription(s)");
    for (auto& subscription : subscriptions) {
        subscription.acknowledged = sendSubscribe(subscription.topic);
    }
    nextSubscribeRetryMs = millis() + MQTT_SUBSCRIBE_RETRY_INTERVAL_MS;
}

void MQTTClientLib::retryPendingSubscriptions() {
    bool pending = false;
    for (const auto& subscription : subscriptions) {
        if (!subscription.acknowledged) {
            pending = true;
            break;
        }
    }
    if (!pending) {
        return;
    }

    // millis() wraps after ~49 days, so compare the signed difference
    if ((int32_t)(millis() - nextSubscribeRetryMs) < 0) {
        return;
    }
    nextSubscribeRetryMs = millis() + MQTT_SUBSCRIBE_RETRY_INTERVAL_MS;

    for (auto& subscription : subscriptions) {
        if (!subscription.acknowledged) {
            subscription.acknowledged = sendSubscribe(subscription.topic);
        }
    }
}

bool MQTTClientLib::subscribe(const String& topic) {
    if (topic.isEmpty()) return false; 

    bool subscribeSuccess = sendSubscribe(topic);
    rememberSubscription(topic, subscribeSuccess);
    return subscribeSuccess;
}

bool MQTTClientLib::subscribe(const std::vector<String>& topics) {
    bool allSubscribed = true;
    for (const auto& topic : topics) {
        bool result = subscribe(topic.c_str());
        allSubscribed = allSubscribed && result;
    }
    return allSubscribed;
}

bool MQTTClientLib::unsubscribe(const String& topic) {
    if (topic.isEmpty()) return false; 

    // Drop it from the restore list first, so a reconnect cannot bring it back
    for (auto it = subscriptions.begin(); it != subscriptions.end(); ++it) {
        if (it->topic == topic) {
            subscriptions.erase(it);
            break;
        }
    }

    bool unsubscribeSuccess = mqttClient.unsubscribe(topic.c_str());
    if (unsubscribeSuccess) {
        Serial.print("Unsubscribed from topic: ");
        Serial.println(topic);
    } else {
        Serial.print("Failed to unsubscribe from topic: ");
        Serial.println(topic);
        Serial.print("Last Error: ");
        Serial.println(mqttClient.lastError());
    }
    return unsubscribeSuccess;
}

// A topic level must not be empty and must not contain wildcards or separators
String MQTTClientLib::sanitizeTopicLevel(const String& value) {
    String result = value;
    result.trim();
    result.replace('/', '_');
    result.replace('+', '_');
    result.replace('#', '_');
    if (result.isEmpty()) {
        return "unknown";
    }
    return result;
}

String MQTTClientLib::jsonEscape(const String& value) {
    String result = value;
    result.replace("\\", "\\\\");
    result.replace("\"", "\\\"");
    return result;
}

String MQTTClientLib::resetReasonName() {
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

bool MQTTClientLib::publishStatus(const String& location, const String& deviceType, const String& deviceName,
                                  const String& firmwareVersion, const String& timestamp,
                                  const String& extraJsonFields) {
    String safeLocation = sanitizeTopicLevel(location);
    String safeType = sanitizeTopicLevel(deviceType);
    String safeName = sanitizeTopicLevel(deviceName);

    String topic = "status/" + safeLocation + "/" + safeType + "/" + safeName;

    // Built by hand instead of via ArduinoJson to keep this library free of that
    // dependency - flash is tight on several of the firmwares using it.
    String payload = "{";
    payload += "\"location\":\"" + jsonEscape(safeLocation) + "\",";
    payload += "\"deviceType\":\"" + jsonEscape(safeType) + "\",";
    payload += "\"deviceName\":\"" + jsonEscape(safeName) + "\",";
    payload += "\"version\":\"" + jsonEscape(firmwareVersion) + "\",";
    payload += "\"mac\":\"" + WiFi.macAddress() + "\",";
    payload += "\"ip\":\"" + WiFi.localIP().toString() + "\",";
    payload += "\"rssi\":" + String(WiFi.RSSI()) + ",";
    payload += "\"uptimeSeconds\":" + String(millis() / 1000) + ",";
    payload += "\"freeHeap\":" + String(ESP.getFreeHeap()) + ",";
    payload += "\"resetReason\":\"" + resetReasonName() + "\",";
    payload += "\"mqttConnects\":" + String(connects);
    if (!timestamp.isEmpty()) {
        payload += ",\"timestamp\":\"" + jsonEscape(timestamp) + "\"";
    }
    if (!extraJsonFields.isEmpty()) {
        payload += "," + extraJsonFields;
    }
    payload += "}";

    return publish(topic, payload, true, 0, false);
}

int MQTTClientLib::lastError() {
    return mqttClient.lastError();
}
