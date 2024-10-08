﻿namespace SharedContracts
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
        /// The current power consumption of the house in Watts
        /// </summary>
        public int HouseConsumptionPower { get; set; }

        /// <summary>
        /// Current power consumption of the inside charging station in Watts
        /// </summary>
        public int InsideCurrentChargingPower { get; set; }

        /// <summary>
        /// Current power consumption of the outside charging station in Watts
        /// </summary>
        public int OutsideCurrentChargingPower { get; set; }

        /// <summary>
        /// Current power generated by the PV in mW
        /// </summary>
        public int PowerFromPV { get; set; }

        /// <summary>
        /// Current power used from the grid in mW
        /// </summary>
        public int PowerFromGrid { get; set; }

        /// <summary>
        /// The power consumed from the battery in mW, is negative if the battery is charged
        /// </summary>
        public int PowerFromBattery { get; set; }

        /// <summary>
        /// The current charge level of the battery in percent  
        /// </summary>
        public int BatteryLevel { get; set; }

        /// <summary>
        /// The power that should be used for charging the car inside in Watts
        /// </summary>
        public int InsideChargingLatestmA { get; set; }

        /// <summary>
        /// The power that should be used for charging the car outside in Watts
        /// </summary>
        public int OutsideChargingLatestmA { get; set; }

        /// <summary>
        /// The current battery level of the BMW in percent
        /// </summary>
        public int BMWBatteryLevel { get; set; }

        /// <summary>
        /// BMW indicates that it is ready for charging
        /// </summary>
        public bool BMWReadyForCharging { get; set; }

        /// <summary>
        /// Indicates when BMW data have been last updated from the server
        /// </summary>
        public DateTimeOffset BMWLastUpdateFromServer { get; set; }

        /// <summary>
        /// The current battery level of the VW in percent
        /// </summary>
        public int VWBatteryLevel { get; set; }

        /// <summary>
        /// VW indicates that it is ready for charging
        /// </summary>
        public bool VWReadyForCharging { get; set; }

        /// <summary>
        /// Indicates when VW data have been last updated from the server
        /// </summary>
        public DateTimeOffset VWLastUpdateFromServer { get; set; }

        public ChargingSituation()
        {
            InsideChargingLatestmA = -1;
            OutsideChargingLatestmA = -1;
        }
    }
}
