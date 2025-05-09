import argparse
import os
import asyncio
import sys
import json
from typing import Dict, Optional, Tuple
import bimmer_connected
import bimmer_connected.vehicle
import paho.mqtt.client as mqtt
from bimmer_connected.account import MyBMWAccount
from bimmer_connected.api.regions import Regions
from bimmer_connected import __version__ as VERSION
from k8s_utils import load_k8s_config, load_oauth_store_from_k8s_secret, store_oauth_store_to_k8s_secret

# MQTT Broker settings
MQTT_BROKER = 'smarthomepi2.fritz.box'
MQTT_PORT = 32004
MQTT_TOPIC = 'data/charging/'

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
        "chargingStatus": vehicle.fuel_and_battery.charging_status,
        "chargingTarget": vehicle.fuel_and_battery.charging_target,
        "chargingEndTime": vehicle.fuel_and_battery.charging_end_time.isoformat() if vehicle.fuel_and_battery.charging_end_time else None,
        "chargerConnected" : vehicle.fuel_and_battery.is_charger_connected,
        "remainingRange": vehicle.fuel_and_battery.remaining_range_electric.value,
        "mileage": vehicle.mileage.value,
        "position": {
            "latitude": vehicle.vehicle_location.location.latitude,
            "longitude": vehicle.vehicle_location.location.longitude,
        },
        "moving": vehicle.is_vehicle_active,
        "lastUpdate": vehicle.timestamp.isoformat(),
    }

# Define a function to save the image to a file
async def save_vehicle_image(vehicle, view_direction):
    filename = f"{vehicle.brand}_{view_direction}.jpg"
    image_bytes = await vehicle.get_vehicle_image(view_direction)
    with open(filename, 'wb') as image_file:
        image_file.write(image_bytes)


async def connect_vehicle(vehicleName: str, parser: argparse.ArgumentParser, interactive: bool) -> Tuple[str, MyBMWAccount, Optional[Dict]]:
    print("##############################################################")
    print(f" - Connecting to {vehicleName} Connected Drive API")
    username = os.getenv(vehicleName + '_USERNAME')
    password = os.getenv(vehicleName + '_PASSWORD')
    vin = os.getenv(vehicleName + '_VIN')

    account = None
    oauth_store_data = {}

    while True:
        try:
            # Attempt to load the token from Kubernetes Secret
            print(f" - Loading OAuth2 tokens from Kubernetes Secret for {vehicleName}")
            account = MyBMWAccount(username, password, Regions.REST_OF_WORLD)
            oauth_store_data = load_oauth_store_from_k8s_secret(vehicleName.lower() + "-connect-api-token", "default", account)
            await account.get_vehicles()
            break  # Exit the loop if successful
        except Exception as e:
            print(f" - Failed to authenticate using Kubernetes Secret for {vehicleName}: {e}")
            if interactive:
                # Prompt user for captcha token if the secret fails and interactive mode is enabled
                captcha_token = input(f"Enter captcha token for {vehicleName}: ").strip()
                try:
                    account = MyBMWAccount(username, password, Regions.REST_OF_WORLD, hcaptcha_token=captcha_token)
                    await account.get_vehicles()
                    # Store the new token in Kubernetes Secret
                    store_oauth_store_to_k8s_secret(vehicleName.lower() + "-connect-api-token", "default", account, oauth_store_data.get("session_id_timestamp"))
                    break  # Exit the loop if successful
                except Exception as captcha_error:
                    print(f" - Failed to authenticate with captcha token for {vehicleName}: {captcha_error}")
            print(f" - Retrying in 10 seconds...")
            await asyncio.sleep(10)  # Wait before retrying

    vehicle = account.get_vehicle(vin)

    print(" - Vehicle Information:")
    print(vehicle.brand, vehicle.name, vehicle.vin)

    return vin, account, oauth_store_data

async def refresh_token_if_needed(account: MyBMWAccount):
    if account.oauth_token.is_expired():
        print(" - OAuth token expired, refreshing...")
        await account.refresh_oauth_token()

async def main():
    print("BMW Connector started")
    print("---------------------")
    print(" - Connecting to MQTT Broker: " + MQTT_BROKER)

    parser = argparse.ArgumentParser(description=f"Connecting to the BMW/Mini API")
    parser.add_argument('--get_vehicle_images', action='store_true', help='Print debug logs')
    parser.add_argument('-interactive', action='store_true', help='Enable interactive mode for captcha token input')
    args = parser.parse_args()

    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.on_connect = on_connect
    client.connect(MQTT_BROKER, MQTT_PORT, 60)
    client.loop_start()

    bmwVin, bmwAccount, bmwOauth_store_data = await connect_vehicle("BMW", parser, args.interactive)
    miniVin, miniAccount, miniOauth_store_data = await connect_vehicle("Mini", parser, args.interactive)

    loop = asyncio.get_event_loop()
    
    payload = None

    while True:       
        try:
            result = await fetch_vehicle_info(bmwAccount, bmwVin)
            payload = json.dumps(result)
            client.publish(MQTT_TOPIC + "BMW", payload, qos=1, retain=True)
            print("Topic :" + MQTT_TOPIC + "BMW" + " | Message Sent: ", payload)
            store_oauth_store_to_k8s_secret("bmw-connect-api-token", "default", bmwAccount, bmwOauth_store_data.get("session_id_timestamp"))
        except Exception as e:
            sys.stderr.write(f"Error: {e}\n")

        try:
            result = await fetch_vehicle_info(miniAccount, miniVin)
            payload = json.dumps(result)
            client.publish(MQTT_TOPIC + "Mini", payload, qos=1, retain=True)
            print("Topic :" + MQTT_TOPIC + "Mini" + " | Message Sent: ", payload)
            store_oauth_store_to_k8s_secret("mini-connect-api-token", "default", miniAccount, miniOauth_store_data.get("session_id_timestamp"))
        except Exception as e:
            sys.stderr.write(f"Error: {e}\n")

        await asyncio.sleep(60)  

    loop.close()
    client.loop_stop()

if __name__ == '__main__':
    asyncio.run(main())
