# BMW CarData — Field Reference

This document maps BMW CarData MQTT fields to the `VehicleState` properties published to the local MQTT broker.

## Currently Mapped Fields

| VehicleState property | BMW field | Description | Type |
|---|---|---|---|
| `battery` | `vehicle.drivetrain.batteryManagement.header` | **Mini only** — real-time HV battery SoC (%) | Number |
| `maxEnergy` | `vehicle.drivetrain.batteryManagement.maxEnergy` | **BMW only** — battery capacity (kWh) | Number |
| `chargingStatus` | `vehicle.drivetrain.electricEngine.charging.status` | NOCHARGING / CHARGINGACTIVE / CHARGINGPAUSED / CHARGINGENDED / CHARGINGERROR | String |
| `hvChargingStatus` | `vehicle.drivetrain.electricEngine.charging.hvStatus` | HV charging status | String |
| `chargingTarget` | `vehicle.powertrain.electric.battery.stateOfCharge.target` | Charge-to target (%, 10% steps). Defaults to 100% if not received (e.g. Mini). | Number |
| `chargingEndTime` | `vehicle.drivetrain.electricEngine.charging.timeRemaining` | Minutes to full charge → converted to UTC timestamp | Number |
| `chargerConnected` | `vehicle.body.chargingPort.status` | CONNECTED / DISCONNECTED | String |
| `remainingRange` | `vehicle.drivetrain.electricEngine.kombiRemainingElectricRange` | Current electric range (km) | Number |
| `predictedRange` | `vehicle.drivetrain.electricEngine.remainingElectricRange` | Predicted range during charging (km) | Number |
| `mileage` | `vehicle.vehicle.travelledDistance` | Current odometer reading (km) | Number |
| `position.latitude` | `vehicle.cabin.infotainment.navigation.currentLocation.latitude` | GPS latitude (degrees) | Number |
| `position.longitude` | `vehicle.cabin.infotainment.navigation.currentLocation.longitude` | GPS longitude (degrees) | Number |
| `moving` | `vehicle.isMoving` | Whether the vehicle is moving | Boolean |
| `chargingPower` | `vehicle.powertrain.electric.battery.charging.power` | Current charging power (W) | Number |
| `chargingMode` | `vehicle.drivetrain.electricEngine.charging.chargingMode` | Current charging mode (e.g. NORMAL_PROGNOSE_BASED) | String |
| `plugEventId` | `vehicle.body.chargingPort.plugEventId` | Increments on each plug-in event | Number |
| `avgConsumption` | `vehicle.drivetrain.avgElectricRangeConsumption` | Average consumption (kWh/100km) | Number |
| `acVoltage` | `vehicle.drivetrain.electricEngine.charging.acVoltage` | Charging voltage (V, AC only) | Number |
| `acAmpere` | `vehicle.drivetrain.electricEngine.charging.acAmpere` | Max charging current (A, AC only) | Number |

## Fields to Subscribe (not yet registered in portal)

### Medium priority
| BMW field | Description | Why useful |
|---|---|---|
| `vehicle.electricalSystem.battery.voltage` | 12V battery voltage | Detect battery health / drain |
| `vehicle.drivetrain.electricEngine.charging.method` | AC_TYPE2 / DC_CCS / etc. | Know if AC or DC charging |
| `vehicle.chassis.axle.row1.wheel.left.tire.pressure` | Tyre pressure front left (kPa) | Safety monitoring |
| `vehicle.chassis.axle.row1.wheel.right.tire.pressure` | Tyre pressure front right (kPa) | Safety monitoring |
| `vehicle.chassis.axle.row2.wheel.left.tire.pressure` | Tyre pressure rear left (kPa) | Safety monitoring |
| `vehicle.chassis.axle.row2.wheel.right.tire.pressure` | Tyre pressure rear right (kPa) | Safety monitoring |

## Fields to Remove from Subscription

| BMW field | Reason |
|---|---|
| `vehicle.drivetrain.lastRemainingRange` | BMW explicitly states: for combustion/PHEV only; will be 0 on BEV |
| `vehicle.powertrain.tractionBattery.charging.port.anyPosition.isPlugged` | Duplicate of `vehicle.body.chargingPort.status` |
| `vehicle.drivetrain.totalRemainingRange` | Meaningful for PHEV only (sum of electric + ICE range) |
| `vehicle.drivetrain.electricEngine.charging.consumptionOverLifeTime.overall.gridEnergy` | Lifetime stat, not useful for real-time home automation |
| `vehicle.trip.segment.accumulated.drivetrain.electricEngine.recuperationTotal` | Niche; recuperation energy per 100km of last trip |
| `vehicle.vehicle.avgSpeed` | Not useful for home automation |

## Notes

- **Real-time SoC**: Available for Mini via `vehicle.drivetrain.batteryManagement.header` (SoC %). Not available for BMW via streaming API.
- **`header` vs `maxEnergy`**: These are different metrics. `header` (Mini) = real-time SoC %. `maxEnergy` (BMW) = battery capacity in kWh. Do not conflate them.
- **Charging target**: Only `stateOfCharge.target` is mapped. Mini doesn't expose this field — defaults to 100%.
- **Charging power unit**: `vehicle.powertrain.electric.battery.charging.power` is in **Watts**, not kW.
- **ChargingEndTime**: Computed at receive time as `DateTime.UtcNow + minutes`. Accuracy degrades if the value isn't refreshed frequently by BMW.
- **Mileage**: `vehicle.vehicle.travelledDistance` is the current live odometer reading.
