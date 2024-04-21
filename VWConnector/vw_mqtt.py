import os
import asyncio
import json
import paho.mqtt.client as mqtt
from weconnect import weconnect
from weconnect.domain import Domain

#MQTT Broker settings
MQTT_BROKER = 'smarthomepi2'
MQTT_PORT = 32004
MQTT_TOPIC = 'data/charging/VW'

def on_connect(client, userdata, flags, rc, properties):
    print(f"Connected with result code {rc}")

async def fetch_vehicle_info():
    username = os.getenv('VW_USERNAME')
    password = os.getenv('VW_PASSWORD')
    carvin = os.getenv('VW_VIN')

    print('#  Initialize WeConnect')
    weConnect = weconnect.WeConnect(username=username, password=password, updateAfterLogin=False, loginOnInit=False)
    weConnect.login()
    weConnect.update()

    vehicle = weConnect.vehicles[carvin]
        
    return {
        "model": vehicle.model.value,
        "battery": int(vehicle.domains['charging']["batteryStatus"].currentSOC_pct.value)
    }

async def main():

    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.on_connect = on_connect
    client.connect(MQTT_BROKER, MQTT_PORT, 60)
    client.loop_start()

    while True:
        result = await fetch_vehicle_info()
        payload = json.dumps(result)
        client.publish(MQTT_TOPIC, payload)
        print("Topic :" + MQTT_TOPIC + " | Message Sent: ", payload)
        await asyncio.sleep(60)  # wait for 60 seconds before the next run

    loop.close()
    client.loop_stop()

if __name__ == '__main__':
    asyncio.run(main())
