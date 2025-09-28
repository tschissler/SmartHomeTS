using SharedContracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartHome.DataHub
{
    internal class Converters
    {
        private Dictionary<string, decimal> previousValues = new Dictionary<string, decimal>();

        public IEnumerable<InfluxRecord> SmartmeterDataToInfluxRecords(SmartmeterData data, string location, string device)
        {
            string gridConsumptionId = SmartmeterData.SensorType.ToString() + "_" + device + "_" + location + "_" + "GridConsumption";
            string gridSupplyId = SmartmeterData.SensorType + "_" + device + "_" + location + "_" + "GridSupply";
            string currentPowerId = SmartmeterData.SensorType + "_" + device + "_" + location + "_" + "CurrentPower";

            // ToDO: Read previous value from DB
            if (!previousValues.ContainsKey(gridConsumptionId))
            {
                previousValues.Add(gridConsumptionId, data.Netzbezug);
            }
            if (!previousValues.ContainsKey(gridSupplyId))
            {
                previousValues.Add(gridSupplyId, data.Netzeinspeisung);
            }
            if (!previousValues.ContainsKey(currentPowerId))
            {
                previousValues.Add(currentPowerId, data.NetzanschlussMomentanleistung);
            }

            var records = new List<InfluxRecord>
            {
                new InfluxEnergyRecord
                {
                    MeasurementId = gridConsumptionId,
                    Category = SmartmeterData.Category,
                    SubCategory = MeasurementSubCategory.Production,
                    SensorType = SmartmeterData.SensorType,
                    Location = location,
                    Device = device,
                    Measurement = "GridConsumption",
                    Value_Cumulated_KWh = data.Netzbezug,
                    Value_Delta_KWh = data.Netzbezug - previousValues[gridConsumptionId],
                    MeasurementType = MeasurementType.Energy
                },
                new InfluxEnergyRecord
                {
                    MeasurementId = gridSupplyId,
                    Category = SmartmeterData.Category,
                    SubCategory = MeasurementSubCategory.Consumption,
                    SensorType = SmartmeterData.SensorType,
                    Location = location,
                    Device = device,
                    Measurement = "GridSupply",
                    Value_Cumulated_KWh = data.Netzeinspeisung,
                    Value_Delta_KWh = data.Netzeinspeisung - previousValues[gridSupplyId],
                    MeasurementType = MeasurementType.Energy
                },
                new InfluxPowerRecord
                {
                    MeasurementId = currentPowerId,
                    Category = SmartmeterData.Category,
                    SubCategory = data.NetzanschlussMomentanleistung < 0 ? "Verbrauch" : "Einspeisung",
                    SensorType = SmartmeterData.SensorType,
                    Location = location,
                    Device = device,
                    Measurement = "CurrentPower",
                    Value_W = data.NetzanschlussMomentanleistung,
                    MeasurementType = MeasurementType.Power
                }
            };
            return records;
        }

        public IEnumerable<InfluxRecord> EnphaseDataToInfluxRecords(EnphaseData data, string location, string device)
        {
            string batteryLevelId = EnphaseData.sensorType + "_" + device + "_" + location + "_" + "BatteryLevel";
            string batteryEnergyId = EnphaseData.sensorType + "_" + device + "_" + location + "_" + "BatteryEnergy";
            string powerFromPVId = EnphaseData.sensorType + "_" + device + "_" + location + "_" + "PowerFromPV";
            string powerFromBatteryId = EnphaseData.sensorType + "_" + device + "_" + location + "_" + "PowerFromBattery";
            string powerFromGridId = EnphaseData.sensorType + "_" + device + "_" + location + "_" + "PowerFromGrid";

            // ToDO: Read previous value from DB
            if (!previousValues.ContainsKey(batteryLevelId))
            {
                previousValues.Add(batteryLevelId, data.BatteryLevel);
            }
            if (!previousValues.ContainsKey(batteryEnergyId))
            {
                previousValues.Add(batteryEnergyId, data.BatteryEnergy / 1000); // Convert Wh to kWh
            }
            if (!previousValues.ContainsKey(powerFromPVId))
            {
                previousValues.Add(powerFromPVId, data.PowerFromPV / 1000); // Convert mW to W
            }
            if (!previousValues.ContainsKey(powerFromBatteryId))
            {
                previousValues.Add(powerFromBatteryId, data.PowerFromBattery / 1000); // Convert mW to W
            }
            if (!previousValues.ContainsKey(powerFromGridId))
            {
                previousValues.Add(powerFromGridId, data.PowerFromGrid / 1000); // Convert mW to W
            }

            var records = new List<InfluxRecord>
            {
                new InfluxPercentageRecord
                {
                    MeasurementId = batteryLevelId,
                    Category = EnphaseData.category,
                    SubCategory = MeasurementSubCategory.Other,
                    SensorType = EnphaseData.sensorType,
                    Location = location,
                    Device = device,
                    Measurement = "BatteryLevel",
                    Value_Percent = data.BatteryLevel,
                    MeasurementType = MeasurementType.Percent
                },
                new InfluxEnergyRecord
                {
                    MeasurementId = batteryEnergyId,
                    Category = EnphaseData.category,
                    SubCategory = MeasurementSubCategory.Other,
                    SensorType = EnphaseData.sensorType,
                    Location = location,
                    Device = device,
                    Measurement = "BatteryEnergy",
                    Value_Cumulated_KWh = data.BatteryEnergy / 1000, // Convert Wh to kWh
                    Value_Delta_KWh = data.BatteryEnergy / 1000 - previousValues[batteryEnergyId],
                    MeasurementType = MeasurementType.Energy
                },
                new InfluxPowerRecord
                {
                    MeasurementId = powerFromPVId,
                    Category = EnphaseData.category,
                    SubCategory = "Produktion",
                    SensorType = EnphaseData.sensorType,
                    Location = location,
                    Device = device,
                    Measurement = "PowerFromPV",
                    Value_W = data.PowerFromPV / 1000, // Convert mW to W
                    MeasurementType = MeasurementType.Power
                },
                new InfluxPowerRecord
                {
                    MeasurementId = powerFromBatteryId,
                    Category = EnphaseData.category,
                    SubCategory = data.PowerFromBattery < 0 ? "Laden" : "Entladen",
                    SensorType = EnphaseData.sensorType,
                    Location = location,
                    Device = device,
                    Measurement = "PowerFromBattery",
                    Value_W = data.PowerFromBattery / 1000, // Convert mW to W
                    MeasurementType = MeasurementType.Power
                },
                new InfluxPowerRecord
                {
                    MeasurementId = powerFromGridId,
                    Category = EnphaseData.category,
                    SubCategory = data.PowerFromGrid < 0 ? "Verbrauch" : "Einspeisung",
                    SensorType = EnphaseData.sensorType,
                    Location = location,
                    Device = device,
                    Measurement = "PowerFromGrid",
                    Value_W = data.PowerFromGrid / 1000, // Convert mW to W
                    MeasurementType = MeasurementType.Power
                },
                new InfluxPowerRecord
                {
                    MeasurementId = EnphaseData.sensorType + "_" + device + "_" + location + "_" + "PowerToHouse",
                    Category = EnphaseData.category,
                    SubCategory = "Verbrauch",
                    SensorType = EnphaseData.sensorType,
                    Location = location,
                    Device = device,
                    Measurement = "PowerToHouse",
                    Value_W = data.PowerToHouse / 1000, // Convert mW to W
                    MeasurementType = MeasurementType.Power
                }
            };
            return records;
        }

        public IEnumerable<InfluxRecord> ShellyPowerDataToInfluxRecords(ShellyPowerData data, string location, string device)
        {
            string powerId = ShellyPowerData.SensorType + "_" + device + "_" + location + "_" + "Power";
            string voltageId = ShellyPowerData.SensorType + "_" + device + "_" + location + "_" + "Voltage";
            string totalPowerId = ShellyPowerData.SensorType + "_" + device + "_" + location + "_" + "TotalPower";

            // ToDO: Read previous value from DB
            if (!previousValues.ContainsKey(powerId))
            {
                previousValues.Add(powerId, data.Power);
            }
            if (!previousValues.ContainsKey(voltageId))
            {
                previousValues.Add(voltageId, data.Voltage);
            }
            if (!previousValues.ContainsKey(totalPowerId))
            {
                previousValues.Add(totalPowerId, data.TotalPower / 1000);
            }

            var deltaTotalPower = data.TotalPower / 1000 >= previousValues[totalPowerId] ? data.TotalPower / 1000 - previousValues[totalPowerId] : data.TotalPower / 1000;
            previousValues[totalPowerId] += deltaTotalPower;

            var records = new List<InfluxRecord>
            {
                new InfluxPowerRecord
                {
                    MeasurementId = powerId,
                    Category = ShellyPowerData.Category,
                    SubCategory = ShellyPowerData.SubCategory.ToString(),
                    SensorType = ShellyPowerData.SensorType,
                    Location = location,
                    Device = device,
                    Measurement = "Power",
                    Value_W = data.Power,
                    MeasurementType = MeasurementType.Power
                },
                new InfluxVoltageRecord
                {
                    MeasurementId = voltageId,
                    Category = ShellyPowerData.Category,
                    SubCategory = ShellyPowerData.SubCategory,
                    SensorType = ShellyPowerData.SensorType,
                    Location = location,
                    Device = device,
                    Measurement = "Voltage",
                    Value_V = data.Voltage,
                    MeasurementType = MeasurementType.Voltage
                },
                new InfluxEnergyRecord
                {
                    MeasurementId = totalPowerId,
                    Category = ShellyPowerData.Category,
                    SubCategory = ShellyPowerData.SubCategory,
                    SensorType = ShellyPowerData.SensorType,
                    Location = location,
                    Device = device,
                    Measurement = "TotalPower",
                    Value_Delta_KWh = deltaTotalPower,
                    Value_Cumulated_KWh = deltaTotalPower + previousValues[totalPowerId],
                    MeasurementType = MeasurementType.Energy
                }
            };
            return records;
        }

        public InfluxTemperatureRecord TemperatureDataToInfluxRecords(decimal value, string location, string subCategory, string meassurement)
        {
            return new InfluxTemperatureRecord
            {
                MeasurementId = "Heizung_" + location + "_" + meassurement,
                Category = MeasurementCategory.Heizung,
                SubCategory = subCategory,
                SensorType = "CanGateway",
                Location = location,
                Device = "Waermepumpe",
                Measurement = meassurement,
                Value_DegreeC = value,
                MeasurementType = MeasurementType.Temperature
            };
        }
    }
}
