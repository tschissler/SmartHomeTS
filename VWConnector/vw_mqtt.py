from datetime import datetime, timedelta
import os
import asyncio
import json
from typing import Optional
from carconnectivity import carconnectivity
from carconnectivity.vehicle import ElectricVehicle
from carconnectivity.commands import GenericCommand
from carconnectivity.command_impl import ChargingStartStopCommand
import paho.mqtt.client as mqtt
from weconnect import weconnect
from weconnect.domain import Domain
from weconnect.elements.plug_status import PlugStatus
from weconnect.elements.charging_status import ChargingStatus
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from typing import List, Optional

    from carconnectivity.garage import Garage

#MQTT Broker settings
MQTT_BROKER = 'smarthomepi2'
MQTT_PORT = 32004
MQTT_TOPIC = 'data/charging/VW'

def on_connect(client, userdata, flags, rc, properties):
    print(f"Connected with result code {rc}")

async def fetch_vehicle_info(carConnect: carconnectivity.CarConnectivity) -> dict:
    carvin = os.getenv('VW_VIN')
    garage: Optional[Garage] = carConnect.get_garage()
    vehicle = garage.get_vehicle(carvin)

    chargingEndTime = None
    if vehicle.charging.state.value.value == ChargingStatus.ChargingState.CHARGING.value :
        chargingEndTime = vehicle.charging.estimated_date_reached.value.isoformat()

    return {
        "nickname": vehicle.name.value,
        "brand": "VW",
        "name": vehicle.model.value,
        "battery": vehicle.drives.drives["primary"].level.value,
        "remainingRange": vehicle.drives.drives["primary"].range.value,
        "mileage": vehicle.odometer.value,
        "chargerConnected": vehicle.charging.connector.connection_state.value.value == "connected",
        "chargingStatus": vehicle.charging.state.value.value,
        "chargingTarget": vehicle.charging.settings.target_level.value,
        "chargingEndTime": chargingEndTime,
        "state": vehicle.state.value.value,
        "position": {
            "latitude": vehicle.position.latitude.value ,
            "longitude": vehicle.position.longitude.value
        },
        "lastUpdate": vehicle.charging.state.last_updated_local.isoformat(),
    }

async def fetch_vehicle_info_weconnect():
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
        
    brand = "VW"
    chargingEndTime = None
    if vehicle.domains['charging']["chargingStatus"].chargingState.value == ChargingStatus.ChargingState.CHARGING :
        chargingEndTime = (datetime.now() + timedelta(minutes=vehicle.domains["charging"]["chargingStatus"].remainingChargingTimeToComplete_min.value)).isoformat()
    return {
        "nickname": vehicle.nickname.value,
        "brand": brand,
        "name": vehicle.model.value,
        "battery": int(vehicle.domains['charging']["batteryStatus"].currentSOC_pct.value),
        "chargingStatus": vehicle.domains['charging']["chargingStatus"].chargingState.value.value,
        "chargingTarget": vehicle.domains['automation']["chargingProfiles"].profiles[1].targetSOC_pct.value,
        "chargingEndTime": chargingEndTime,
        "chargerConnected": vehicle.domains["charging"]["plugStatus"].plugConnectionState.value == PlugStatus.PlugConnectionState.CONNECTED,
        "remainingRange": vehicle.domains['charging']["batteryStatus"].cruisingRangeElectric_km.value,
        "mileage": vehicle.domains['measurements']["odometerStatus"].odometer.value,

        "lastUpdate": vehicle.domains['charging']["batteryStatus"].currentSOC_pct.lastUpdateFromServer.isoformat(),
    }

async def main():

    print("Starting VW Connector")
    print("##################################")
    print()
    print("Connecting to MQtT broker " + MQTT_BROKER + " on port " + str(MQTT_PORT) + " ...")
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.on_connect = on_connect
    client.connect(MQTT_BROKER, MQTT_PORT, 60)
    client.loop_start()
    print("successfully connected")

    print("-----------------------------------")
    print("Connecting to VW API ...")
    username = os.getenv('VW_USERNAME')
    password = os.getenv('VW_PASSWORD')
    carvin = os.getenv('VW_VIN')

    car_connectivity_config = {
        "carConnectivity": {
            "connectors": [
                {
                    "type": "volkswagen",
                    "config": {
                        "username": username,
                        "password": password
                    }
                },
            ]
        }
    }

    carConnect = carconnectivity.CarConnectivity(config=car_connectivity_config, tokenstore_file="tokenstore.json")
    carConnect.fetch_all()
    garage: Optional[Garage] = carConnect.get_garage()
    vehicle = garage.get_vehicle(carvin)
    print("successfully connected for vehicle " + vehicle.name.value)

    while True:
        result = await fetch_vehicle_info(carConnect)
        payload = json.dumps(result)
        client.publish(MQTT_TOPIC, payload, qos=1, retain=True)
        print("Topic :" + MQTT_TOPIC + " | Message Sent: ", payload)
        await asyncio.sleep(60)  # wait for 60 seconds before the next run

    loop.close()
    client.loop_stop()

if __name__ == '__main__':
    asyncio.run(main())
