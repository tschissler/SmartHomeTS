# BMW CarData — Field Reference

This document maps BMW CarData MQTT fields to the `VehicleState` properties published to the local MQTT broker.

The payload goes to `daten/Fahrzeug/<Vehicle>/Status`, retained, and is read on the other side
as `SharedContracts.CarStatusData`. **Every property listed below has a counterpart there** —
before backlog item 10 it did not, and the ones without a counterpart were dropped by
`System.Text.Json` without a word. `BMWConnectorTests/VehicleStatePayloadTests` pins the two
ends together, so adding a row here without adding the property to `CarStatusData` is a red
test rather than a value that quietly disappears.

Which of these the vehicle actually delivers is a separate question: a field has to be
registered in the CarData portal first. `SETUP.md` lists what is registered today and what is
only mapped.

| VehicleState property | BMW field | Description | Type |
|---|---|---|---|
| `Zeitpunkt` | — | Publication time of this message (UTC, ISO 8601). Mandatory field of the topic convention; not a vehicle value. **Not the same as `lastUpdate`** — that is when the vehicle measured, this is when the connector sent. | String |
| `battery` | `vehicle.drivetrain.batteryManagement.header` | Real-time HV battery SoC (%) — **delivered by both BMW and Mini** | Number |
| `maxEnergy` | `vehicle.drivetrain.batteryManagement.maxEnergy` | **BMW only** — battery capacity (kWh) | Number |
| `chargingStatus` | `vehicle.drivetrain.electricEngine.charging.status` | NOCHARGING / CHARGINGACTIVE / CHARGINGPAUSED / CHARGINGENDED / CHARGINGERROR | String |
| `hvChargingStatus` | `vehicle.drivetrain.electricEngine.charging.hvStatus` | HV charging status | String |
| `chargingTarget` | `vehicle.powertrain.electric.battery.stateOfCharge.target` | Charge-to target (%, 10% steps). Defaults to 100% if not received (e.g. Mini). | Number |
| `chargingEndTime` | `vehicle.drivetrain.electricEngine.charging.timeRemaining` | Minutes to full charge → converted to UTC timestamp. **Note: BMW does not send this field via streaming API** — field stays absent. | Number |
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

- **Real-time SoC**: Available for **both BMW and Mini** via `vehicle.drivetrain.batteryManagement.header` (SoC %). Verified on the BMW on 2026-09-20 (83 % measured). An earlier note here claimed this field was Mini-only — that was wrong.
- **`header` vs `maxEnergy`**: These are different metrics. `header` (BMW + Mini) = real-time SoC %. `maxEnergy` (BMW only) = battery capacity in kWh. Do not conflate them.
- **Charging target**: Only `stateOfCharge.target` is mapped. Mini doesn't expose this field — `ChargingTarget` defaults to 100%. **Open question after backlog item 10:** every other value is now null when the vehicle did not report it, and the interface shows "—" rather than inventing a number. This one still invents 100. Either register `stateOfCharge.targetMin` for the Mini, or make `ChargingTarget` nullable like the rest — not decided, deliberately left as it was.
- **Charging power unit**: `vehicle.powertrain.electric.battery.charging.power` is in **Watts**, not kW.
- **ChargingEndTime**: Computed from `timeRemaining` if sent. BMW streaming API does not send this field — `chargingEndTime` will not appear in the payload. Set to null when not charging.
- **Mileage**: `vehicle.vehicle.travelledDistance` is the current live odometer reading.
