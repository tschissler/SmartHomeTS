# Service reading vehicle data from BWM / Mini Connected and publish them as MQTT messages

The service uses the [bimmer_connected library](https://github.com/bimmerconnected/bimmer_connected) to communicate with the BWM / Mini Connected Service.

## Supporting multiple vehicles
Currently the service supports a BMW and a Mini vehickle as you can see in the main() method.
But you can easily adjust this to any number of vehicles and you can obviously also have multiple vehicles of the same brand.

## Connecting / Login
To login into the Connected Service you have to provide your username and password along with the VIN (Vehicle Identification Number) of your vehicle
Currently the service reads those data from environment variables 
- [Vehiclename]_USERNAME
- [Vehiclename]_PASSWORD
- [Vehiclename]_VIN

Additionally the API requires a Captcha-Token to ensure you are not a robot. You can acquire the token here:
- [North America](https://bimmer-connected.readthedocs.io/en/stable/captcha/north_america.html)
- [Rest of World](https://bimmer-connected.readthedocs.io/en/stable/captcha/rest_of_world.html)

  You can provide this token as a commandline argument --captcha_token_BMW / --captcha_token_Mini

Once the connection is established, you do not need the Captcha-Token for further access during the lifetime of the connection. But to survive a reeboot, you can store a Refresh-Token. In the current implementation, the Refresh-Token is stored as a Kubernetes Secret but you can also store it in a file or somewhere else. If the Captcha-Token is not provided, the service loads the Refresh-Token and uses this to connect to the cloud.

## Datacollection and sending as MQTT message
The current implementation collects the following data and sends a MQTT message with it:

``` python
        "brand": vehicle.brand,
        "name": vehicle.name,
        "battery": vehicle.fuel_and_battery.remaining_battery_percent,
        "chargingstatus": vehicle.fuel_and_battery.charging_status,
        "chargingtarget": vehicle.fuel_and_battery.charging_target,
        "chargerconnected" : vehicle.fuel_and_battery.is_charger_connected,
        "remainingrange": vehicle.fuel_and_battery.remaining_range_electric,
        "mileage": vehicle.mileage,
        "moving": vehicle.is_vehicle_active,

        "last_update": vehicle.timestamp.isoformat(),
```

You can find more available data on the [bimmer documentation](https://bimmer-connected.readthedocs.io/en/stable/module/vehicle.html).
