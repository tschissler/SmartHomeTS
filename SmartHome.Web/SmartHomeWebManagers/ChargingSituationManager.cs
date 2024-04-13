using DataContracts;

namespace SmartHomeWebManagers
{
    public class ChargingSituationManager
    {
        private const int PowerMaximum = 15000;

        public static AvailabilityData CalculateAvailabilityData(int pvPower, int batteryPower, int gridPower)
        {
            var availabilityData = new AvailabilityData
            {
                PVPowerPercent = CalculatePowerPercent(pvPower),
                BatteryPowerPercent = CalculatePowerPercent(batteryPower),
                GridPowerPercent = CalculatePowerPercent(gridPower)
            };

            return availabilityData;
        }

        public static ConsumptionData CalculateConsumptionData(int houseConsumption, int batteryPower, int gridPower, int garageCharging, int outsideCharging)
        {
            var consumptionData = new ConsumptionData
            {
                HouseConsumption = CalculatePowerPercent(houseConsumption),
                BatteryCharging = CalculatePowerPercent(batteryPower * -1),
                GridFeed = CalculatePowerPercent( gridPower * -1),
                GarageCharging = CalculatePowerPercent(garageCharging),
                OutsideCharging = CalculatePowerPercent(outsideCharging)
            };

            return consumptionData;
        }

        private static decimal CalculatePowerPercent(int power)
        {
            if (power < 0)
                return 0;
            return (power * 100) / PowerMaximum;
        }
    }
}
