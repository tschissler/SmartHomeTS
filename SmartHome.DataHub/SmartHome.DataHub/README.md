# Data Structure

## Influx DB

Data in the Influx DB is stored in tables.
There will be one table per measurement type. 
This list describes the used tables and their structures:

### energy_values

This table stores all energy measurements, electricity, heat and others

|category|sub_category|device|location|measurement|sensor_type|time|value_kwh|value_cumulated_kwh|
|--|--|--|--|--|--|--|--|--|
|The category of the measurement, e.g. Electricity, Heat|E.g. consumption, production or other|The name of the device the measurement is assigned to|The location of the device|The measurement. A device can provide multiple measurements|The type of the sensor, e.g. Shelly, SMLSensor|The timestamp of the measurement|The value of the measurement in KWh as provided by the device. Caution, some devices like the Shelly Plugs will restart the measurement at different occasions.|The cumulative energy of the measurement. The cumulation can start at the first usage of the device or when recording started|

