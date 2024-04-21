import os
from flask import Flask, jsonify, request
import asyncio
from bimmer_connected.account import MyBMWAccount
from bimmer_connected.api.regions import Regions

app = Flask(__name__)

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
        "battery": vehicle.fuel_and_battery.remaining_battery_percent
    }

@app.route('/battery', methods=['GET'])
def battery():  
    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)
    result = loop.run_until_complete(fetch_vehicle_info())
    loop.close()

    return jsonify(result)

if __name__ == '__main__':
    app.run(debug=True)
