namespace ShellyConnector.DataContracts
{
    public record ShellyPowerData
    (
        double Power,
        double Voltage,
        double TotalPower,
        DateTimeOffset Timestamp
    );
}
