#ifndef MQTTCLIENTLIB_H
#define MQTTCLIENTLIB_H

#include <WiFi.h>
#include <MQTT.h>
#include <vector>

// Define the maximum packet size for the MQTT client
#define MQTT_MAX_PACKET_SIZE 8192

// How long to wait before retrying a subscription the broker did not acknowledge
#define MQTT_SUBSCRIBE_RETRY_INTERVAL_MS 5000

// Self-healing: if the main task stops calling loop() - stuck in a blocking socket
// operation, a frozen library call, anything - the task watchdog restarts the device.
// A reboot every few minutes beats a device that stays dead until someone unplugs it.
#define MQTT_LIB_WATCHDOG_TIMEOUT_MS 300000

// connect() blocks while it retries. If the broker stays unreachable this long, the
// device restarts instead of looping forever - a restart recovers stuck network state
// that a retry from the same socket cannot.
#define MQTT_CONNECT_RESTART_AFTER_MS 300000

// A connection that is established and then dies again within seconds is a failure mode
// neither of the two guards above can see: connect() returns successfully and loop() keeps
// being called, yet nothing the device sends ever reaches the broker. Count connects that
// follow the previous one within this window and restart once reconnecting is all the
// device still does.
#define MQTT_RECONNECT_STORM_WINDOW_MS 60000
#define MQTT_RECONNECT_STORM_LIMIT 10


class MQTTClientLib {
public:
    MQTTClientLib(const String& mqtt_broker, int mqtt_port, const String& clientId, WiFiClient& wifiClient, MQTTClientCallbackSimple callback);
    void connect(bool cleanSession);
    bool connected();

    // Publishes a retained device heartbeat on status/<location>/<deviceType>/<deviceName>.
    // Call it periodically (once a minute is plenty). A consumer can tell a silent device
    // from a healthy one by the age of this message, which a last will cannot do - a device
    // that is connected but no longer processing anything keeps its connection open.
    // timestamp is optional; without it a consumer has to fall back on its own receive time,
    // which is misleading for a retained message picked up long after it was sent.
    // extraJsonFields is appended verbatim, e.g. "\"relays\":{\"WC\":\"OFF\"}".
    bool publishStatus(const String& location, const String& deviceType, const String& deviceName,
                       const String& firmwareVersion, const String& timestamp = "",
                       const String& extraJsonFields = "");

    // Number of successful connects since boot; the first connect counts as one
    uint32_t connectCount() const { return connects; }
    bool loop();
    bool publish(const String &topic, const String &payload, bool retained, int qos, bool printLogMessages = true);
    bool subscribe(const String& topic);
    bool subscribe(const std::vector<String>& topics);
    bool unsubscribe(const String &topic);
    int lastError();

private:
    // Every topic that was subscribed to at least once, together with the information
    // whether the broker has acknowledged it on the current connection. The client
    // restores these itself after a reconnect instead of relying on a broker side
    // session, which is silently lost whenever the broker restarts.
    struct Subscription {
        String topic;
        bool acknowledged;
    };

    void armWatchdog();
    void feedWatchdog();
    bool sendSubscribe(const String& topic);
    static String sanitizeTopicLevel(const String& value);
    static String jsonEscape(const String& value);
    static String resetReasonName();
    void rememberSubscription(const String& topic, bool acknowledged);
    void resubscribeAll();
    void retryPendingSubscriptions();

    MQTTClient mqttClient;
    String clientId;
    String mqtt_broker;
    std::vector<Subscription> subscriptions;
    uint32_t nextSubscribeRetryMs = 0;
    uint32_t connects = 0;
    uint32_t lastConnectMs = 0;
    uint32_t rapidReconnects = 0;
    uint32_t lastDataPublishMs = 0;
    bool hasPublishedData = false;
    bool watchdogArmed = false;
};

#endif // MQTTCLIENTLIB_H
