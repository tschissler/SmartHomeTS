namespace SharedContracts
{
    public record ChargingSetData()
    {
        /// <summary>
        /// The current in mA
        /// </summary>
        public int ChargingCurrent { get ; set; }
    }

    public record ChargingGetData()
    {
        /// <summary>
        /// Itentifies if the car is pluged in and ready for charging
        /// </summary>
        public bool CarIsPlugedIn { get; set; }
        /// <summary>
        /// The current charging power in mW 
        /// </summary>
        public int CurrentChargingPower { get; set; }
        /// <summary>
        ///  The enegy loaded in the current charging session in Wh
        /// </summary>
        public int EnergyCurrentChargingSession { get; set; }
        /// <summary>
        ///  The total energy loaded in Wh
        /// </summary>
        public int EnergyTotal {  get; set; }
    }
}
