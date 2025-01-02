import argparse
import os
import asyncio
import json
from kubernetes import client, config
from pathlib import Path
import sys
import time
from typing import Dict, Optional
import paho.mqtt.client as mqtt
from bimmer_connected.account import MyBMWAccount
from bimmer_connected.api.regions import Regions
from bimmer_connected import __version__ as VERSION
import base64

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

def load_oauth_store_from_file(oauth_store: Path, account: MyBMWAccount) -> Dict:
    """Load the OAuth details from a file if it exists."""
    if not oauth_store.exists():
        print(f"OAuth store file {oauth_store} does not exist")
        return {}
    try:
        oauth_data = json.loads(oauth_store.read_text())
    except json.JSONDecodeError:
        return {}

    session_id_timestamp = oauth_data.pop("session_id_timestamp", None)
    # Pop session_id every 14 days to it gets recreated
    if (time.time() - (session_id_timestamp or 0)) > 14 * 24 * 60 * 60:
        oauth_data.pop("session_id", None)
        session_id_timestamp = None

    account.set_refresh_token(**oauth_data)

    return {**oauth_data, "session_id_timestamp": session_id_timestamp}

def load_oauth_store_from_envvariable(envVariableName: str, account: MyBMWAccount) -> Dict:
    """Load the OAuth details from an environment variable if it exists."""
    if envVariableName not in os.environ:
        print(f"Environment variable {envVariableName} not set")
        return {}
    try:
        oauth_data_raw = os.getenv(envVariableName).replace('\\"', '"')
        oauth_data = json.loads(oauth_data_raw)
    except json.JSONDecodeError:
        print(f"Error loading OAuth data from environment variable {oauth_data_raw}")
        return {}

    print("Loaded OAuth data from environment variable")
    session_id_timestamp = oauth_data.pop("session_id_timestamp", None)
    # Pop session_id every 14 days to it gets recreated
    if (time.time() - (session_id_timestamp or 0)) > 14 * 24 * 60 * 60:
        oauth_data.pop("session_id", None)
        session_id_timestamp = None

    account.set_refresh_token(**oauth_data)

    return {**oauth_data, "session_id_timestamp": session_id_timestamp}

def store_oauth_store_to_file(
    oauth_store: Path, account: MyBMWAccount, session_id_timestamp: Optional[float] = None
) -> None:
    """Store the OAuth details to a file."""
    oauth_store.parent.mkdir(parents=True, exist_ok=True)
    oauth_store.write_text(
        json.dumps(
            {
                "refresh_token": account.config.authentication.refresh_token,
                "gcid": account.config.authentication.gcid,
                "access_token": account.config.authentication.access_token,
                "session_id": account.config.authentication.session_id,
                "session_id_timestamp": session_id_timestamp or time.time(),
            }
        ),
    )

def load_k8s_config():
    """Load Kubernetes configuration based on the environment."""
    try:
        config.load_incluster_config()
    except config.ConfigException:
        config.load_kube_config()

def load_oauth_store_from_k8s_secret(secret_name: str, namespace: str, account: MyBMWAccount) -> Dict:
    """Read the OAuth details from a Kubernetes secret."""
    print(f"Loading OAuth data from Kubernetes secret {secret_name} in namespace {namespace}")
    load_k8s_config()
    v1 = client.CoreV1Api()

    try:
        secret = v1.read_namespaced_secret(secret_name, namespace)
        secret_data = secret.data

        oauth_data = {
            "refresh_token": base64.b64decode(secret_data["refresh_token"]).decode('utf-8'),
            "gcid": base64.b64decode(secret_data["gcid"]).decode('utf-8'),
            "access_token": base64.b64decode(secret_data["access_token"]).decode('utf-8'),
            "session_id": base64.b64decode(secret_data["session_id"]).decode('utf-8'),
            "session_id_timestamp": float(base64.b64decode(secret_data["session_id_timestamp"]).decode('utf-8')),
        }

        #print(f"Loaded OAuth data from Kubernetes secret {oauth_data}")
        session_id_timestamp = oauth_data.pop("session_id_timestamp", None)
        # Pop session_id every 14 days to it gets recreated
        if (time.time() - (session_id_timestamp or 0)) > 14 * 24 * 60 * 60:
            oauth_data.pop("session_id", None)
            session_id_timestamp = None

        account.set_refresh_token(**oauth_data)
        
        return {**oauth_data, "session_id_timestamp": session_id_timestamp}

    except client.exceptions.ApiException as e:
        if e.status == 404:
            raise ValueError(f"Secret {secret_name} not found in namespace {namespace}")
        else:
            raise e

def store_oauth_store_to_k8s_secret(
    secret_name: str, namespace: str, account: MyBMWAccount, session_id_timestamp: Optional[float] = None
) -> None:
    """Store the OAuth details in a Kubernetes secret."""
    #print(f"Storing OAuth data in Kubernetes secret {secret_name} in namespace {namespace}")
    load_k8s_config()
    v1 = client.CoreV1Api()

    #print(f"refresh_token: {account.config.authentication.refresh_token} | gcid: {account.config.authentication.gcid} | access_token: {account.config.authentication.access_token} | session_id: {account.config.authentication.session_id} | session_id_timestamp: {session_id_timestamp or time.time()}")

    secret_data = {
        "refresh_token": base64.b64encode(account.config.authentication.refresh_token.encode()).decode('utf-8'),
        "gcid": base64.b64encode(account.config.authentication.gcid.encode()).decode('utf-8'),
        "access_token": base64.b64encode(account.config.authentication.access_token.encode()).decode('utf-8'),
        "session_id": base64.b64encode(account.config.authentication.session_id.encode()).decode('utf-8'),
        "session_id_timestamp": base64.b64encode(str(session_id_timestamp or time.time()).encode()).decode('utf-8'),
    }

    secret = client.V1Secret(
        metadata=client.V1ObjectMeta(name=secret_name),
        data=secret_data,
    )

    try:
        v1.read_namespaced_secret(secret_name, namespace)
        v1.replace_namespaced_secret(secret_name, namespace, secret)
    except client.exceptions.ApiException as e:
        if e.status == 404:
            v1.create_namespaced_secret(namespace, secret)
        else:
            raise e

def on_connect(client, userdata, flags, rc, properties):
    print(f"Connected with result code {rc}")

async def fetch_vehicle_info(account: MyBMWAccount, vin: str) -> Dict:
    await account.get_vehicles()
    vehicle = account.get_vehicle(vin)
    return {
        "brand": vehicle.brand,
        "name": vehicle.name,
        "battery": vehicle.fuel_and_battery.remaining_battery_percent,
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
    username = os.getenv('BMW_USERNAME')
    password = os.getenv('BMW_PASSWORD')
    vin = os.getenv('BMW_VIN')

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
