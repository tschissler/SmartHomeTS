import argparse
import os
import asyncio
import sys
import json
from typing import Dict, Optional
import paho.mqtt.client as mqtt
from bimmer_connected.account import MyBMWAccount
from bimmer_connected.api.regions import Regions
from bimmer_connected import __version__ as VERSION
from k8s_utils import load_k8s_config, load_oauth_store_from_k8s_secret, store_oauth_store_to_k8s_secret

# MQTT Broker settings
MQTT_BROKER = 'smarthomepi2.fritz.box'
MQTT_PORT = 32004
MQTT_TOPIC = 'data/charging/BMW'

def main_parser() -> argparse.ArgumentParser:
    """Create the ArgumentParser with all relevant subparsers."""
    parser = argparse.ArgumentParser(
        description=(f"Connect to MyBMW/MINI API and interact with your vehicle.\n\nVersion: {VERSION}"),
        formatter_class=argparse.RawTextHelpFormatter,
    )
    parser.add_argument("--debug", help="Print debug logs.", action="store_true")
    parser.add_argument(
        "--oauth-store",
        help="Path to the OAuth2 storage file. Defaults to $HOME/.bimmer_connected.json.",
        nargs="?",
        metavar="FILE",
        type=Path,
        default=Path.home() / ".bimmer_connected.json",
    )
    parser.add_argument("--disable-oauth-store", help="Disable storing the OAuth2 tokens.", action="store_true")

 
    return parser

def on_connect(client, userdata, flags, rc, properties):
    print(f"Connected with result code {rc}")

async def fetch_vehicle_info(account: MyBMWAccount, vin: str) -> Dict:
    await account.get_vehicles()
    vehicle = account.get_vehicle(vin)
    return {
        "brand": vehicle.brand,
        "name": vehicle.name,
        "battery": vehicle.fuel_and_battery.remaining_battery_percent,
        "mileage": vehicle.mileage,
        "last_update": vehicle.timestamp.isoformat(),
    }

async def main():
    print("BMW Connector started")
    print("---------------------")
    print(" - Connecting to MQTT Broker: " + MQTT_BROKER)

    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.on_connect = on_connect
    client.connect(MQTT_BROKER, MQTT_PORT, 60)
    client.loop_start()

    print("")
    print(" - Connecting to BMW Connected Drive API")
    username = os.getenv('MINI_USERNAME')
    password = os.getenv('MINI_PASSWORD')
    vin = os.getenv('MINI_VIN')

    print(f" - Username: {username} | Password: {password} | VIN: {vin}")
    parser = argparse.ArgumentParser(description='Connecting to the BMW API')
    parser.add_argument('--captcha_token', type=str, required=False, help='Captcha token for BMW')
    args = parser.parse_args()
    captcha_token = args.captcha_token
    account = MyBMWAccount(username, password, Regions.REST_OF_WORLD, hcaptcha_token=captcha_token)
    #oauth_store_data = load_oauth_store_from_file(Path("bimmer.oauth"), account)
    oauth_store_data = {}
    if (captcha_token is None):
        oauth_store_data = load_oauth_store_from_k8s_secret("bmw-connect-api-token", "default", account)

    loop = asyncio.get_event_loop()
    
    payload = None

    while True:       
        try:
            result = await fetch_vehicle_info(account, vin)
            payload = json.dumps(result)
            client.publish(MQTT_TOPIC, payload)
            print("Topic :" + MQTT_TOPIC + " | Message Sent: ", payload)
            #store_oauth_store_to_file(Path("bimmer.oauth"), account, oauth_store_data.get("session_id_timestamp"))
            store_oauth_store_to_k8s_secret("bmw-connect-api-token", "default", account, oauth_store_data.get("session_id_timestamp"))
        except Exception as e:
            sys.stderr.write(f"Error: {e}\n")
            #sys.exit(1)

        await asyncio.sleep(10)  

    loop.close()
    client.loop_stop()

if __name__ == '__main__':
    asyncio.run(main())
