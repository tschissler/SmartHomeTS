namespace ShellyConnector.DataContracts
{
    public class ShellyThermostatStatus
    {
        public int Id { get; set; }
        public decimal Target_C { get; set; }
        public decimal Current_C { get; set; }
        public int Pos { get; set; }
        public bool Connected { get; set; }
        public int Rssi { get; set; }
        public int Battery { get; set; }
        public int Last_updated_ts { get; set; }
    }
}