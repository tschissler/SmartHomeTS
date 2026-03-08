import paho.mqtt.client as mqtt
import ssl

GCID     = "91c1203d-1ad2-47f0-8828-ab4fc137877f"
ID_TOKEN = open("/home/thomas/.local/state/bmw-mqtt-bridge/id_token.txt").read().strip()

def on_connect(client, userdata, flags, reason_code, properties):
    print(f"CONNACK: {reason_code} ({reason_code.getName()})")
    if reason_code == 0:
        print("Connected! Subscribing...")
        client.subscribe(f"{GCID}/+")
    else:
        client.disconnect()

def on_message(client, userdata, msg):
    print(f"\nTOPIC: {msg.topic}")
    print(f"PAYLOAD: {msg.payload.decode()}")

def on_disconnect(client, userdata, flags, reason_code, properties):
    print(f"Disconnected: {reason_code}")

client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2, protocol=mqtt.MQTTv5)
client.username_pw_set(GCID, ID_TOKEN)
client.tls_set(cert_reqs=ssl.CERT_REQUIRED, tls_version=ssl.PROTOCOL_TLS_CLIENT)
client.on_connect    = on_connect
client.on_message    = on_message
client.on_disconnect = on_disconnect

print(f"Connecting...")
client.connect("customer.streaming-cardata.bmwgroup.com", 9000, 60)
client.loop_forever()
