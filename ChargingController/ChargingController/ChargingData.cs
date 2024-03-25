namespace ChargingController
{
    public record ChargingResult (
            int InsideChargingPowerWatts,
            int OutsideChargingPowerWatts
        );

    public record ChargingInput (
            bool InsideConnected,
            bool OutsideConnected,
            int InsideCurrentChargingPower,
            int OutsideCurrentChargingPower,
            int GridPower,
            ChargingStation PreferedChargingStation,
            int MaximumGridChargingPercent
        );

    public enum ChargingStation
    {
        None = 0,
        Inside,
        Outside
    }
}
