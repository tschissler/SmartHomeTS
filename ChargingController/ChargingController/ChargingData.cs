namespace ChargingController
{
    public record ChargingResult (
            int InsideChargingPowerWatts,
            int OutsideChargingPowerWatts,
            int InsideChargingCurrentmA,
            int OutsideChargingCurrentmA
        );

    public class ChargingInput()
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
        /// Current power consumption of the outside charging station in Watts
        /// </summary>
        public int OutsideCurrentChargingPower { get; set; }
        /// <summary>
        /// Current power used from the grid in Watts
        /// </summary>
        public int GridPower { get; set; }
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
        public int PrefereChargingBatteryLevel { get; set; }
    }

    public enum ChargingStation
    {
        None = 0,
        Inside,
        Outside
    }
}
