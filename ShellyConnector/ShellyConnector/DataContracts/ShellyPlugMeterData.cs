namespace ShellyConnector.DataContracts
{
    public class ShellyPlugMeterData
    {
        public decimal Power { get; set; }
        public decimal Overpower { get; set; }
        public bool Is_Valid { get; set; }
        public int Timestamp { get; set; }
        public List<decimal> Counters { get; set; }
        public decimal Total { get; set; }
    }
}
