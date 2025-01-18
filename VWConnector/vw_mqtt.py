from datetime import datetime, timedelta
import os
import asyncio
import json
import paho.mqtt.client as mqtt
from weconnect import weconnect
from weconnect.domain import Domain
from weconnect.elements.plug_status import PlugStatus

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

    #weConnect.update(updateCapabilities=False, updatePictures=False, selective=[]) 
    #allElements = weConnect.getLeafChildren()
    #for element in allElements:
    #    print(element.getGlobalAddress())
    
    vehicle = weConnect.vehicles[carvin]
        
    brand_code = vehicle.brandCode.value.value
    if brand_code == 'V':
        brand = "VW"
    elif brand_code == 'N':
        brand = "ID Buzz"
    else:
        brand = "Unknown"

    return {
        "nickname": vehicle.nickname.value,
        "brand": brand,
        "name": vehicle.model.value,
        "battery": int(vehicle.domains['charging']["batteryStatus"].currentSOC_pct.value),
        "chargingStatus": vehicle.domains['charging']["chargingStatus"].chargingState.value.value,
        "chargingTarget": vehicle.domains['automation']["chargingProfiles"].profiles[1].targetSOC_pct.value,
        "chargingEndTime": (datetime.now() + timedelta(minutes=vehicle.domains["charging"]["chargingStatus"].remainingChargingTimeToComplete_min.value)).isoformat(),
        "chargerConnected": vehicle.domains["charging"]["plugStatus"].plugConnectionState.value.value == PlugStatus.PlugConnectionState.CONNECTED,
        "remainingRange": vehicle.domains['charging']["batteryStatus"].cruisingRangeElectric_km.value,
        "mileage": vehicle.domains['measurements']["odometerStatus"].odometer.value,

        "last_Update": vehicle.domains['charging']["batteryStatus"].currentSOC_pct.lastUpdateFromServer.isoformat(),
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
