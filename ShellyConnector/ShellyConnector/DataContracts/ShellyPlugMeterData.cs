﻿namespace ShellyConnector.DataContracts
{
    public class ShellyPlugMeterData
    {
        public double Power { get; set; }
        public double Overpower { get; set; }
        public bool IsValid { get; set; }
        public int Timestamp { get; set; }
        public List<double> Counters { get; set; }
        public double Total { get; set; }
    }
}