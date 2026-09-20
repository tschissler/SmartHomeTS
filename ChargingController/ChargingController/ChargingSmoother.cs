using SharedContracts;

namespace ChargingController
{
    /// <summary>
    /// Exponential moving average over the power readings that feed the charging decision.
    /// Short consumption spikes (kettle, oven cycles, heat pump start) and PV dips from
    /// passing clouds are damped away, so the decision follows real trends only.
    /// </summary>
    /// <remarks>
    /// All four readings that make up the available power are filtered with the same time
    /// constant. Because the available power is a linear combination of them, filtering the
    /// parts is equivalent to filtering the result, and simultaneous steps (e.g. the car
    /// stops charging, so the grid reading changes as well) stay consistent with each other.
    /// </remarks>
    public class ChargingSmoother
    {
        private readonly TimeSpan timeConstant;

        private DateTimeOffset lastUpdate = DateTimeOffset.MinValue;
        private double powerFromGrid;
        private double powerFromBattery;
        private double insideChargingPower;
        private double outsideChargingPower;

        public ChargingSmoother(TimeSpan timeConstant)
        {
            this.timeConstant = timeConstant;
        }

        /// <summary>
        /// Returns a copy of the situation with the power readings replaced by their filtered
        /// values. The passed situation is not modified: it keeps the raw values, which are
        /// published for the UI and are the input of the next filter step.
        /// </summary>
        public ChargingSituation Smooth(ChargingSituation situation, DateTimeOffset now)
        {
            if (lastUpdate == DateTimeOffset.MinValue)
            {
                // The first reading initialises the filter, otherwise it would ramp up from zero
                powerFromGrid = situation.PowerFromGrid;
                powerFromBattery = situation.PowerFromBattery;
                insideChargingPower = situation.InsideCurrentChargingPower;
                outsideChargingPower = situation.OutsideCurrentChargingPower;
            }
            else
            {
                var alpha = CalculateAlpha(now - lastUpdate);
                powerFromGrid += alpha * (situation.PowerFromGrid - powerFromGrid);
                powerFromBattery += alpha * (situation.PowerFromBattery - powerFromBattery);
                insideChargingPower += alpha * (situation.InsideCurrentChargingPower - insideChargingPower);
                outsideChargingPower += alpha * (situation.OutsideCurrentChargingPower - outsideChargingPower);
            }
            lastUpdate = now;

            var smoothed = situation.Clone();
            smoothed.PowerFromGrid = (int)Math.Round(powerFromGrid);
            smoothed.PowerFromBattery = (int)Math.Round(powerFromBattery);
            smoothed.InsideCurrentChargingPower = (int)Math.Round(insideChargingPower);
            smoothed.OutsideCurrentChargingPower = (int)Math.Round(outsideChargingPower);
            return smoothed;
        }

        /// <summary>
        /// Time based filter weight, so the filter behaves the same whether the control cycle
        /// runs on time or was delayed. After a long gap alpha approaches 1 and the filter
        /// simply adopts the new reading.
        /// </summary>
        private double CalculateAlpha(TimeSpan elapsed)
        {
            if (elapsed <= TimeSpan.Zero)
                return 0;
            if (timeConstant <= TimeSpan.Zero)
                return 1;
            return 1 - Math.Exp(-elapsed.TotalSeconds / timeConstant.TotalSeconds);
        }
    }
}
