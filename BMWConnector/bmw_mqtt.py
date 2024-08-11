import os
import asyncio
import json
import paho.mqtt.client as mqtt
from bimmer_connected.account import MyBMWAccount
from bimmer_connected.api.regions import Regions

# MQTT Broker settings
MQTT_BROKER = 'smarthomepi2.fritz.box'
MQTT_PORT = 32004
MQTT_TOPIC = 'data/charging/BMW'

def on_connect(client, userdata, flags, rc, properties):
    print(f"Connected with result code {rc}")

async def fetch_vehicle_info():
    username = os.getenv('BMW_USERNAME')
    password = os.getenv('BMW_PASSWORD')
    vin = os.getenv('BMW_VIN')

    account = MyBMWAccount(username, password, Regions.REST_OF_WORLD)
    await account.get_vehicles()
    vehicle = account.get_vehicle(vin)
    return {
        "brand": vehicle.brand,
        "name": vehicle.name,
        "battery": vehicle.fuel_and_battery.remaining_battery_percent,
        "last_update": vehicle.timestamp.isoformat(),
    }

async def main():
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.on_connect = on_connect
    client.connect(MQTT_BROKER, MQTT_PORT, 60)
    client.loop_start()

    print("BMW Connector started")
    while True:
        result = await fetch_vehicle_info()
        payload = json.dumps(result)
        client.publish(MQTT_TOPIC, payload)
        print("Topic :" + MQTT_TOPIC + " | Message Sent: ", payload)
        await asyncio.sleep(900)  # wait for 15 minutes before the next run

    loop.close()
    client.loop_stop()

if __name__ == '__main__':
    asyncio.run(main())
