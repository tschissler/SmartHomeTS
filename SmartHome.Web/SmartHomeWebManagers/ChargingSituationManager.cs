namespace SmartHomeWebManagers
{
    public class ChargingSituationManager
    {
        /// <summary>
        /// Full scale of both bars on the charging page, in watt: a share of this size fills a
        /// bar completely.
        /// </summary>
        /// <remarks>
        /// It is a scale, not a limit. Measured against 90 days of EnvoyM3 readings, the total
        /// of a bar -- both bars divide up one and the same total, see Docs/Ladeprotokoll.md --
        /// stays below 10 kW 99.7 % of the time (median 0.6 kW, p95 7.5 kW, p99 8.9 kW) and
        /// peaked at 18.1 kW. The scale used to be 15 kW, which left the bar at a fifth of its
        /// width on an ordinary day.
        ///
        /// Above the scale the shares add up to more than 100 %. Nothing clamps them (see
        /// <see cref="CalculatePowerPercent"/>), the flex container shrinks the segments into
        /// the width it has, and the bar keeps showing the proportions correctly while losing
        /// the absolute measure. ChargingOverview marks that case at the right end of the bar --
        /// without it, 10 kW and 18 kW draw exactly the same picture.
        /// </remarks>
        public const int PowerMaximum = 10000;

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
        ///
        /// It clamps downwards but deliberately not upwards: a share above 100 % is the only
        /// thing that tells the page the scale has been left. Clamping here would make the
        /// overflow unrecoverable further up -- the bar would be full either way and the mark
        /// could never appear.
        /// </remarks>
        private static decimal CalculatePowerPercent(int power)
        {
            if (power < 0)
                return 0;
            return (power * 100m) / PowerMaximum;
        }
    }
}
