from datetime import datetime, timezone
import os
import asyncio
import json
import threading
from typing import Optional
from carconnectivity import carconnectivity
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

# The EU Data Act portal produces a new dataset roughly every 15 minutes.
# Polling faster than that only burns API calls, it does not yield fresher data.
POLL_INTERVAL_SECONDS = int(os.getenv('VW_POLL_INTERVAL', '900'))

# Connector tuning. country/language are only part of the OIDC state, brand
# selects the OIDC client_id and the manufacturer label.
VW_COUNTRY = os.getenv('VW_COUNTRY', 'de')
VW_LANGUAGE = os.getenv('VW_LANGUAGE', 'de')
VW_BRAND = os.getenv('VW_BRAND', 'VOLKSWAGEN_PASSENGER_CARS')

# Readiness/health staleness threshold. With a 900 s poll interval this allows
# four consecutive poll cycles to fail (or the portal to deliver nothing usable)
# before Kubernetes considers the pod unready. The portal is known to be flaky
# and to serve "no_content_found" datasets, and restarting the pod does not fix
# a portal-side outage, so the threshold is deliberately generous.
HEALTH_STALE_SECONDS = int(os.getenv('VW_HEALTH_STALE_SECONDS', '3600'))

# Health state tracking
_last_poll_attempt = None       # last completed poll cycle (drives readiness)
_last_successful_fetch = None   # last time a valid payload was published
_is_mqtt_connected = False

# Last payload that was actually published. Used to fill in individual values
# the portal did not deliver in the current dataset, so a partial dataset never
# resets the dashboard to zero.
_last_published_payload = None


def _now():
    return datetime.now(timezone.utc)


def _age_seconds(timestamp):
    if timestamp is None:
        return None
    return (_now() - timestamp).total_seconds()


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

    def _is_healthy():
        poll_age = _age_seconds(_last_poll_attempt)
        return _is_mqtt_connected and (poll_age is None or poll_age < HEALTH_STALE_SECONDS)

    @app.route('/healthz')
    def liveness():
        return jsonify({"status": "alive"}), 200

    @app.route('/ready')
    def readiness():
        if _is_healthy():
            return jsonify({"status": "ready"}), 200
        return jsonify({"status": "not ready"}), 503

    @app.route('/health')
    def health():
        is_healthy = _is_healthy()
        poll_age = _age_seconds(_last_poll_attempt)
        data_age = _age_seconds(_last_successful_fetch)
        last_poll_ago = f"{poll_age:.0f} seconds ago" if poll_age is not None else None
        last_data_ago = f"{data_age:.0f} seconds ago" if data_age is not None else None
        if _last_poll_attempt is None:
            description = "Service is starting up"
        elif is_healthy:
            description = f"Last poll: {last_poll_ago}, last valid data: {last_data_ago or 'never'}"
        else:
            description = f"MQTT: {_is_mqtt_connected}, Last poll: {last_poll_ago}, last valid data: {last_data_ago or 'never'}"
        return jsonify({
            "status": "Healthy" if is_healthy else "Unhealthy",
            "checks": [{
                "name": "vw_connector",
                "status": "Healthy" if is_healthy else "Unhealthy",
                "description": description
            }]
        }), 200 if is_healthy else 503

    app.run(host='0.0.0.0', port=port, threaded=True)

def on_connect(client, userdata, flags, rc, properties):
    global _is_mqtt_connected
    _is_mqtt_connected = True
    print(f"Connected with result code {rc}")


# The EU Data Act export never carries the connector's connection_state, so the
# plug state is derived from the charging state instead. The portal only reports
# these while a cable is attached.
CONNECTED_CHARGING_STATES = frozenset({
    'charging', 'ready_for_charging', 'conservation', 'discharging',
})
# 'off' is the only state that positively indicates no charging relationship.
DISCONNECTED_CHARGING_STATES = frozenset({'off'})


def _connected_from_charging_state(charging_status):
    """Infer whether the charge cable is plugged in from the charging state.

    Returns None for states that say nothing about the plug (error, unsupported,
    unknown) so the caller keeps the last known value instead of asserting False.
    """
    if charging_status in CONNECTED_CHARGING_STATES:
        return True
    if charging_status in DISCONNECTED_CHARGING_STATES:
        return False
    return None


def _value_of(attribute):
    """Return attribute.value, tolerating a missing attribute entirely."""
    if attribute is None:
        return None
    return getattr(attribute, 'value', None)


def _enum_value_of(attribute):
    """Return the plain string behind an EnumAttribute, or None."""
    value = _value_of(attribute)
    if value is None:
        return None
    return getattr(value, 'value', value)


def _iso(dt):
    """Format a datetime the way Telegraf expects (RFC3339 with offset).

    json_time_format in mqtt_import.conf is "2006-01-02T15:04:05Z07:00", so the
    timestamp must always carry an explicit UTC offset. Naive values are assumed
    to be UTC and everything is converted to local time to keep the previous
    behaviour (which used last_updated_local) unchanged.
    """
    if dt is None:
        return None
    if dt.tzinfo is None:
        dt = dt.replace(tzinfo=timezone.utc)
    return dt.astimezone().isoformat()


def _last_updated(attribute):
    """Best-effort 'when was this measured' timestamp of an attribute."""
    if attribute is None:
        return None
    return getattr(attribute, 'last_updated', None)


def _get_electric_drive(vehicle):
    """The vehicle's electric drive, whatever slot the connector put it in.

    The EU Data Act connector uses drive id 'primary' for a pure EV but
    'secondary' for a PHEV, so the old hard-coded drives["primary"] lookup is no
    longer safe.
    """
    getter = getattr(vehicle, 'get_electric_drive', None)
    if getter is not None:
        try:
            drive = getter()
            if drive is not None:
                return drive
        except Exception:
            pass
    drives = getattr(getattr(vehicle, 'drives', None), 'drives', None) or {}
    for drive_id in ('primary', 'secondary'):
        if drive_id in drives:
            return drives[drive_id]
    return None


def _merge_with_last_known(payload):
    """Replace values the portal did not deliver with the last published ones.

    The EU Data Act portal regularly serves partial or "no_content_found"
    datasets. Publishing a None for e.g. mileage would break the consumers:
    CarStatusData in SmartHome.Web maps battery/remainingRange/mileage/
    chargingTarget onto non-nullable doubles, and System.Text.Json throws on a
    JSON null for those. So every optional value falls back to the last known
    good one and only then to a type-safe default.
    """
    defaults = {
        "nickname": "",
        "name": "",
        "remainingRange": 0,
        "mileage": 0,
        "chargerConnected": False,
        "chargingStatus": "unknown charging state",
        "chargingTarget": 0,
        "state": "unknown vehicle state",
    }
    for key, default in defaults.items():
        if payload.get(key) is None:
            previous = (_last_published_payload or {}).get(key)
            payload[key] = previous if previous is not None else default
    return payload


async def fetch_vehicle_info(carConnect: carconnectivity.CarConnectivity) -> Optional[dict]:
    """Poll the portal and build the MQTT payload.

    Returns None when nothing usable came back. The caller must then keep the
    previous retained message instead of overwriting it with empty values.
    """
    try:
        carvin = os.getenv('VW_VIN')
        carConnect.fetch_all()
        garage: Optional[Garage] = carConnect.get_garage()
        if garage is None:
            print("No garage available from car connectivity, skipping publish")
            return None
        vehicle = garage.get_vehicle(carvin)
        if vehicle is None:
            print(f"Vehicle {carvin} not found in garage, skipping publish")
            return None

        drive = _get_electric_drive(vehicle)
        battery = _value_of(getattr(drive, 'level', None))

        # The EU Data Act datasets for some models (e.g. ID.4, issue #33) carry
        # no per-drive range; the combined range is the only value available.
        remaining_range = _value_of(getattr(drive, 'range', None))
        if remaining_range is None:
            remaining_range = _value_of(getattr(getattr(vehicle, 'drives', None), 'total_range', None))

        charging = getattr(vehicle, 'charging', None)
        charging_status = _enum_value_of(getattr(charging, 'state', None))
        connector = getattr(charging, 'connector', None)
        connection_state = _enum_value_of(getattr(connector, 'connection_state', None))
        if connection_state is not None:
            charger_connected = connection_state == "connected"
        else:
            charger_connected = _connected_from_charging_state(charging_status)

        charging_end_time = None
        if charging_status == "charging":
            charging_end_time = _iso(_value_of(getattr(charging, 'estimated_date_reached', None)))

        # The connector never sets vehicle.state (the portal has no ignition or
        # driving state). Fall back to the parking brake, which it does map.
        state = _enum_value_of(getattr(vehicle, 'state', None))
        if state is None and _value_of(getattr(vehicle, 'parking_brake', None)) is True:
            state = "parked"

        # Dataset freshness. The connector exposes a custom 'captured_at'
        # attribute holding the portal's capture time; that is the most accurate
        # source for lastUpdate. Fall back to attribute update times.
        last_update = _value_of(getattr(vehicle, 'captured_at', None))
        if last_update is None:
            last_update = _last_updated(getattr(charging, 'state', None))
        if last_update is None:
            last_update = _last_updated(getattr(drive, 'level', None))
        if last_update is None:
            last_update = _last_updated(getattr(vehicle, 'odometer', None))

        # State of charge is the core value of this integration. Without it (and
        # without a timestamp Telegraf can use) the dataset is worthless and must
        # not overwrite the retained message.
        if battery is None or last_update is None:
            print("Portal returned no usable dataset (no state of charge / timestamp), keeping last retained values")
            return None

        payload = {
            "nickname": _value_of(getattr(vehicle, 'name', None)),
            "brand": "VW",
            # The EU Data Act connector does not report a model name, so fall
            # back to the portal nickname.
            "name": _value_of(getattr(vehicle, 'model', None)) or _value_of(getattr(vehicle, 'name', None)),
            "battery": battery,
            "remainingRange": remaining_range,
            "mileage": _value_of(getattr(vehicle, 'odometer', None)),
            "chargerConnected": charger_connected,
            "chargingStatus": charging_status,
            "chargingTarget": _value_of(getattr(getattr(charging, 'settings', None), 'target_level', None)),
            "chargingEndTime": charging_end_time,
            "state": state,
            # NOTE: "position" is intentionally omitted. The EU Data Act export
            # contains no GPS data at all, and emitting nulls would break
            # SharedContracts.GeoPosition (non-nullable doubles). Omitting the
            # key leaves the C# default GeoPosition(0,0) in place; no consumer
            # (Web UI, Flutter app, Telegraf) reads the field.
            "lastUpdate": _iso(last_update),
        }
        return _merge_with_last_known(payload)
    except Exception as e:
        print("Error fetching vehicle info:", e)
        return None

def validate_env_vars():
    required_vars = ['VW_USERNAME', 'VW_PASSWORD', 'VW_VIN']
    for var in required_vars:
        if not os.getenv(var):
            raise EnvironmentError(f"Environment variable '{var}' is not set.")

async def main():
    global _last_poll_attempt, _last_successful_fetch, _last_published_payload, _is_mqtt_connected

    validate_env_vars()

    # Display version information on startup
    version_info = get_version_info()
    version_display = f"Version: {version_info['version']} | Build: {version_info['buildNumber']} | Commit: {version_info['gitCommit']} | Date: {version_info['buildDate']}"
    print("╔════════════════════════════════════════════════════════════════════╗")
    print("║  VWConnector Starting                                              ║")
    print("╠════════════════════════════════════════════════════════════════════╣")
    print(f"║  {version_display:<66}║")
    print("╚════════════════════════════════════════════════════════════════════╝")

    print(f" ### Configuration: MQTT Broker={MQTT_BROKER}:{MQTT_PORT}, Health Check Port={HEALTH_CHECK_PORT}, "
          f"Poll interval={POLL_INTERVAL_SECONDS}s, Health staleness threshold={HEALTH_STALE_SECONDS}s")

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
    print("Connecting to VW EU Data Act portal ...")
    username = os.getenv('VW_USERNAME')
    password = os.getenv('VW_PASSWORD')
    carvin = os.getenv('VW_VIN')

    carConnect: Optional[carconnectivity.CarConnectivity] = None

    # The WeConnect API was shut down by VW/Cariad; the EU Data Act portal
    # (eu-data-act.drivesomethinggreater.com) is the remaining access path. It is
    # strictly read-only and requires a continuous 15-minute data request to be
    # configured in the portal beforehand.
    car_connectivity_config = {
        "carConnectivity": {
            "connectors": [
                {
                    "type": "vw_eu_data_act",
                    "config": {
                        "username": username,
                        "password": password,
                        "interval": POLL_INTERVAL_SECONDS,
                        "country": VW_COUNTRY,
                        "language": VW_LANGUAGE,
                        "brand": VW_BRAND,
                        "vin": carvin,
                    }
                },
            ]
        }
    }

    # The portal is regularly unavailable or slow to provision the first
    # dataset. Retrying beats exiting, which previously put the pod into
    # CrashLoopBackOff and kept it there.
    attempt = 0
    while carConnect is None:
        attempt += 1
        try:
            carConnect = carconnectivity.CarConnectivity(config=car_connectivity_config, tokenstore_file="tokenstore.json")
            carConnect.fetch_all()
            garage: Optional[Garage] = carConnect.get_garage()
            if garage is None:
                raise RuntimeError("Garage not available from car connectivity")
            vehicle = garage.get_vehicle(carvin)
            if vehicle is None:
                raise RuntimeError(f"Vehicle with VIN {carvin} not found in portal account")
            print(f"successfully connected for vehicle {_value_of(getattr(vehicle, 'name', None))}")
        except Exception as e:
            carConnect = None
            backoff = min(60 * attempt, POLL_INTERVAL_SECONDS)
            print(f"Error connecting to VW EU Data Act portal (attempt {attempt}): {e} - retrying in {backoff}s")
            await asyncio.sleep(backoff)

    while True:
        try:
            result = await fetch_vehicle_info(carConnect)
            if result is None:
                # Keep the previous retained message rather than clearing the
                # dashboard with empty values.
                print("Skipping publish, no usable data from portal")
            else:
                payload = json.dumps(result)
                client.publish(MQTT_TOPIC, payload, qos=1, retain=True)
                _last_published_payload = result
                _last_successful_fetch = _now()
                print("Topic :" + MQTT_TOPIC + " | Message Sent: ", payload)
        except Exception as e:
            print("Error:", e)
        finally:
            _last_poll_attempt = _now()

        await asyncio.sleep(POLL_INTERVAL_SECONDS)

if __name__ == '__main__':
    asyncio.run(main())
