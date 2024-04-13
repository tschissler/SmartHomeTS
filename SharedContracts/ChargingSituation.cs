namespace SharedContracts
{
    public record ChargingResult (
            int InsideChargingPowerWatts,
            int OutsideChargingPowerWatts,
            int InsideChargingCurrentmA,
            int OutsideChargingCurrentmA
        );

    public class ChargingSituation
    {
        /// <summary>
        /// Is a car connected to the inside charging station and ready for charging
        /// </summary>
        public bool InsideConnected { get; set; }

        /// <summary>
        /// Is a car connected to the outside charging station and ready for charging
        /// </summary>
        public bool OutsideConnected { get; set; }

        /// <summary>
        /// Current power consumption of the inside charging station in Watts
        /// </summary>
        public int InsideCurrentChargingPower { get; set; }

        /// <summary>
        /// Current power consumption of the outside charging station in mW
        /// </summary>
        public int OutsideCurrentChargingPower { get; set; }

        /// <summary>
        /// Current power used from the grid in mW
        /// </summary>
        public int GridPower { get; set; }

        /// <summary>
        /// The power consumed from the battery in mW, is negative if the battery is charged
        /// </summary>
        public int PowerFromBattery { get; set; }

        /// <summary>
        /// The charging station that should be used first if both are available
        /// None-prefered station is used if the available power exceeds the minimum charging power of the first station (6A)
        /// </summary>
        public ChargingStation PreferedChargingStation { get; set; }

        /// <summary>
        /// The maximum percentage of the grid power that can be used for charging
        /// </summary>
        public int MaximumGridChargingPercent { get; set; }

        /// <summary>
        /// The current charge level of the battery in percent  
        /// </summary>
        public int BatteryLevel { get; set; }

        /// <summary>
        /// The minimum charge level of the battery in percent where the car should be charged
        /// Below this level, charging will be started only if there is enough energy provided by PV
        /// </summary>
        public int BatteryMinLevel { get; set; }

        /// <summary>
        /// The level of battery charge in percent below which the battery should be charged before putting energy in the car
        /// </summary>
        public int PreferedChargingBatteryLevel { get; set; }

        /// <summary>
        /// This setting ovverides all calculated values and sets the charging current to the given value in mA
        /// If the value is lower than 0, the calculated value is used
        /// </summary>
        public int ManualCurrent { get; set; }

        /// <summary>
        /// The power that should be used for charging the car inside in Watts
        /// </summary>
        public int InsideChargingLatestmA { get; set; }

        /// <summary>
        /// The power that should be used for charging the car outside in Watts
        /// </summary>
        public int OutsideChargingLatestmA { get; set; }

        public ChargingSituation()
        {
            InsideChargingLatestmA = -1;
            OutsideChargingLatestmA = -1;
        }
    }

    public enum ChargingStation
    {
        None = 0,
        Inside,
        Outside
    }
}
