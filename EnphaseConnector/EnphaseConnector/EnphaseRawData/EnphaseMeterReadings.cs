namespace EnphaseConnector.EnphaseRawData
{
    public record EnphaseMeterReading(
        int Eid,
        long Timestamp,
        double ActEnergyDlvd,
        double ActEnergyRcvd
    );
}
