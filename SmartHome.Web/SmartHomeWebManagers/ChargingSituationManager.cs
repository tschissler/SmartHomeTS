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

        /// <summary>
        /// Share of one value in the bar, in percent of <see cref="PowerMaximum"/>.
        /// </summary>
        /// <remarks>
        /// The 100 is a decimal on purpose. Both operands used to be int, so the division was
        /// an integer one and the result only became a decimal afterwards -- the fraction never
        /// existed. The bar was therefore quantised to steps of 150 W: everything below that
        /// vanished from it while the legend next to it still read "Batterie laden: 6 W", and
        /// 1.350 W and 1.499 W drew exactly the same segment.
        /// </remarks>
        private static decimal CalculatePowerPercent(int power)
        {
            if (power < 0)
                return 0;
            return (power * 100m) / PowerMaximum;
        }
    }
}
