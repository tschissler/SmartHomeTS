from datetime import datetime, timedelta
import os
import asyncio
import json
import threading
from typing import Optional
from carconnectivity import carconnectivity
from carconnectivity.vehicle import ElectricVehicle, Charging
from carconnectivity.commands import GenericCommand
from carconnectivity.command_impl import ChargingStartStopCommand
import paho.mqtt.client as mqtt
from flask import Flask, jsonify
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from typing import List, Optional

    from carconnectivity.garage import Garage

# Configuration from environment variables
MQTT_BROKER = os.getenv('VW_MQTT_BROKER', 'smarthomepi2')
MQTT_PORT = int(os.getenv('VW_MQTT_PORT', '32004'))
MQTT_TOPIC = 'data/charging/VW'
HEALTH_CHECK_PORT = int(os.getenv('VW_HEALTH_CHECK_PORT', '8080'))

# Health state tracking
_last_successful_fetch = None
_is_mqtt_connected = False

def get_version_info():
    """Read version info from version.json (created during Docker build)."""
    try:
        version_file = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'version.json')
        if os.path.exists(version_file):
            with open(version_file, 'r') as f:
                return json.load(f)
    except Exception:
        pass
    return {
        "version": "0.0.0",
        "buildDate": "development",
        "gitCommit": "dev",
        "buildNumber": "dev"
    }

def start_health_server(port):
    """Start Flask health check server in a background thread."""
    app = Flask(__name__)

    @app.route('/healthz')
    def liveness():
        return jsonify({"status": "alive"}), 200

    @app.route('/ready')
    def readiness():
        if _is_mqtt_connected and (_last_successful_fetch is None or
            (datetime.utcnow() - _last_successful_fetch).total_seconds() < 300):
            return jsonify({"status": "ready"}), 200
        return jsonify({"status": "not ready"}), 503

    @app.route('/health')
    def health():
        is_healthy = _is_mqtt_connected and (_last_successful_fetch is None or
            (datetime.utcnow() - _last_successful_fetch).total_seconds() < 300)
        last_fetch_ago = None
        if _last_successful_fetch:
            last_fetch_ago = f"{(datetime.utcnow() - _last_successful_fetch).total_seconds():.0f} seconds ago"
        return jsonify({
            "status": "Healthy" if is_healthy else "Unhealthy",
            "checks": [{
                "name": "vw_connector",
                "status": "Healthy" if is_healthy else "Unhealthy",
                "description": "Service is starting up" if _last_successful_fetch is None
                    else (last_fetch_ago if is_healthy
                    else f"MQTT: {_is_mqtt_connected}, Last fetch: {last_fetch_ago}")
            }]
        }), 200 if is_healthy else 503

    app.run(host='0.0.0.0', port=port, threaded=True)

def on_connect(client, userdata, flags, rc, properties):
    global _is_mqtt_connected
    _is_mqtt_connected = True
    print(f"Connected with result code {rc}")

async def fetch_vehicle_info(carConnect: carconnectivity.CarConnectivity) -> dict:
    try:
        carvin = os.getenv('VW_VIN')
        carConnect.fetch_all()
        garage: Optional[Garage] = carConnect.get_garage()
        vehicle = garage.get_vehicle(carvin)
        chargingEndTime = None
        ChargingState = type(vehicle.charging.state.value)
        if vehicle.charging.state.value == ChargingState.CHARGING.value and vehicle.charging.estimated_date_reached.value is not None:
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
    except Exception as e:
        print("Error fetching vehicle info:", e)
        return {}

def validate_env_vars():
    required_vars = ['VW_USERNAME', 'VW_PASSWORD', 'VW_VIN']
    for var in required_vars:
        if not os.getenv(var):
            raise EnvironmentError(f"Environment variable '{var}' is not set.")

async def main():
    global _last_successful_fetch, _is_mqtt_connected

    validate_env_vars()

    # Display version information on startup
    version_info = get_version_info()
    version_display = f"Version: {version_info['version']} | Build: {version_info['buildNumber']} | Commit: {version_info['gitCommit']} | Date: {version_info['buildDate']}"
    print("╔════════════════════════════════════════════════════════════════════╗")
    print("║  VWConnector Starting                                              ║")
    print("╠════════════════════════════════════════════════════════════════════╣")
    print(f"║  {version_display:<66}║")
    print("╚════════════════════════════════════════════════════════════════════╝")

    print(f" ### Configuration: MQTT Broker={MQTT_BROKER}:{MQTT_PORT}, Health Check Port={HEALTH_CHECK_PORT}")

    # Start health check server in background thread
    health_thread = threading.Thread(target=start_health_server, args=(HEALTH_CHECK_PORT,), daemon=True)
    health_thread.start()
    print(f" ### Health check server starting on port {HEALTH_CHECK_PORT}")

    print(f"Connecting to MQTT broker {MQTT_BROKER} on port {MQTT_PORT} ...")
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

    carConnect: Optional[carconnectivity.CarConnectivity] = None

    try:
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
        if garage is None:
            raise RuntimeError("Garage not available from car connectivity")
        vehicle = garage.get_vehicle(carvin)
        if vehicle is None:
            raise RuntimeError("Vehicle with provided VIN not found")
        print(f"successfully connected for vehicle {vehicle.name.value}")
    except Exception as e:
        print("Error connecting to VW API:", e)
        return

    if carConnect is None:
        print("VW API connection not available; stopping connector.")
        return

    while True:
        try:
            result = await fetch_vehicle_info(carConnect)
            payload = json.dumps(result)
            client.publish(MQTT_TOPIC, payload, qos=1, retain=True)
            _last_successful_fetch = datetime.utcnow()
            print("Topic :" + MQTT_TOPIC + " | Message Sent: ", payload)
            await asyncio.sleep(60)  # wait for 60 seconds before the next run

        except Exception as e:
            print("Error:", e)

if __name__ == '__main__':
    asyncio.run(main())
