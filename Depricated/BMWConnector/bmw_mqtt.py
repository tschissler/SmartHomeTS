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

# MQTT Broker settings (configurable via environment variables)
MQTT_BROKER = os.getenv('BMW_MQTT_BROKER', 'mosquitto.intern')
MQTT_PORT = int(os.getenv('BMW_MQTT_PORT', '1883'))
MQTT_TOPIC = 'data/charging/'

# Polling configuration
POLL_INTERVAL = 900   # 15 minutes — BMW rate-limits heavily
MAX_BACKOFF = 7200    # 2 hours maximum backoff on repeated errors

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

async def connect_vehicle(vehicleName: str, interactive: bool) -> Tuple[str, MyBMWAccount, Optional[Dict]]:
    print("##############################################################")
    print(f" - Connecting to {vehicleName} Connected Drive API")
    username = os.getenv(vehicleName + '_USERNAME')
    password = os.getenv(vehicleName + '_PASSWORD')
    vin = os.getenv(vehicleName + '_VIN')

    account = None
    oauth_store_data = {}

    while True:
        try:
            print(f" - Loading OAuth2 tokens from Kubernetes Secret for {vehicleName}")
            account = MyBMWAccount(username, password, Regions.REST_OF_WORLD)
            oauth_store_data = load_oauth_store_from_k8s_secret(vehicleName.lower() + "-connect-api-token", "smarthome", account)
            await account.get_vehicles()
            break
        except Exception as e:
            print(f" - Failed to authenticate using Kubernetes Secret for {vehicleName}: {e}")
            if interactive:
                captcha_token = input(f"Enter captcha token for {vehicleName}: ").strip()
                try:
                    account = MyBMWAccount(username, password, Regions.REST_OF_WORLD, hcaptcha_token=captcha_token)
                    await account.get_vehicles()
                    store_oauth_store_to_k8s_secret(vehicleName.lower() + "-connect-api-token", "smarthome", account, oauth_store_data.get("session_id_timestamp"))
                    break
                except Exception as captcha_error:
                    print(f" - Failed to authenticate with captcha token for {vehicleName}: {captcha_error}")
            print(f" - Retrying in 10 seconds...")
            await asyncio.sleep(10)

    vehicle = account.get_vehicle(vin)

    print(" - Vehicle Information:")
    print(vehicle.brand, vehicle.name, vehicle.vin)

    return vin, account, oauth_store_data

async def refresh_token_if_needed(account: MyBMWAccount) -> bool:
    """Refresh the OAuth token if expired. Returns True if the token was refreshed."""
    if account.oauth_token.is_expired():
        print(" - OAuth token expired, refreshing...")
        await account.refresh_oauth_token()
        return True
    return False

async def main():
    print("BMW Connector started")
    print("---------------------")
    print(f" - Connecting to MQTT Broker: {MQTT_BROKER}:{MQTT_PORT}")

    parser = argparse.ArgumentParser(description=f"Connecting to the BMW/Mini API")
    parser.add_argument('-interactive', action='store_true', help='Enable interactive mode for captcha token input')
    args = parser.parse_args()

    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.on_connect = on_connect
    client.connect(MQTT_BROKER, MQTT_PORT, 60)
    client.loop_start()

    bmwVin, bmwAccount, bmwOauth_store_data = await connect_vehicle("BMW", args.interactive)
    miniVin, miniAccount, miniOauth_store_data = await connect_vehicle("Mini", args.interactive)

    backoff = 0

    while True:
        errors = 0

        try:
            token_refreshed = await refresh_token_if_needed(bmwAccount)
            result = await fetch_vehicle_info(bmwAccount, bmwVin)
            payload = json.dumps(result)
            client.publish(MQTT_TOPIC + "BMW", payload, qos=1, retain=True)
            print("Topic :" + MQTT_TOPIC + "BMW" + " | Message Sent: ", payload)
            if token_refreshed:
                store_oauth_store_to_k8s_secret("bmw-connect-api-token", "smarthome", bmwAccount, bmwOauth_store_data.get("session_id_timestamp"))
        except Exception as e:
            errors += 1
            sys.stderr.write(f"Error fetching BMW data: {e}\n")

        try:
            token_refreshed = await refresh_token_if_needed(miniAccount)
            result = await fetch_vehicle_info(miniAccount, miniVin)
            payload = json.dumps(result)
            client.publish(MQTT_TOPIC + "Mini", payload, qos=1, retain=True)
            print("Topic :" + MQTT_TOPIC + "Mini" + " | Message Sent: ", payload)
            if token_refreshed:
                store_oauth_store_to_k8s_secret("mini-connect-api-token", "smarthome", miniAccount, miniOauth_store_data.get("session_id_timestamp"))
        except Exception as e:
            errors += 1
            sys.stderr.write(f"Error fetching Mini data: {e}\n")

        if errors > 0:
            backoff = min(backoff * 2 if backoff > 0 else POLL_INTERVAL * 2, MAX_BACKOFF)
            print(f" - {errors} error(s) this cycle, backing off for {backoff}s before next poll")
            await asyncio.sleep(backoff)
        else:
            backoff = 0
            await asyncio.sleep(POLL_INTERVAL)

if __name__ == '__main__':
    asyncio.run(main())
