using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedContracts
{
    public class ClimateData
    {
        public DataPoint BasementTemperature { get; set; }
        public DataPoint BasementHumidity { get; set; }
        public DataPoint OutsideTemperature { get; set; }
        public DataPoint OutsideHumidity { get; set; }
        public DataPoint ChildRoomTemperature { get; set; }
        public DataPoint ChildRoomHumidity { get; set; }
        public DataPoint BathRoomM1Temperature { get; set; }
        public DataPoint BathRoomM1Humidity { get; set; }
        public DataPoint LivingRoomTemperature { get; set; }
        public DataPoint LivingRoomHumidity { get; set; }
        public DataPoint BedroomTemperature { get; set; }
        public DataPoint BedroomHumidity { get; set; }
        public DataPoint CisternFillLevel { get; set; }

        public ClimateData()
        {
            BasementTemperature = new(0m, DateTimeOffset.Now);
            BasementHumidity = new(0m, DateTimeOffset.Now);
            OutsideTemperature = new(0m, DateTimeOffset.Now);
            OutsideHumidity = new(0m, DateTimeOffset.Now);
            ChildRoomTemperature = new(0m, DateTimeOffset.Now);
            ChildRoomHumidity = new(0m, DateTimeOffset.Now);
            BathRoomM1Temperature = new(0m, DateTimeOffset.Now);
            BathRoomM1Humidity = new(0m, DateTimeOffset.Now);
            LivingRoomTemperature = new(0m, DateTimeOffset.Now);
            LivingRoomHumidity = new(0m, DateTimeOffset.Now);
            BedroomTemperature = new(0m, DateTimeOffset.Now);
            BedroomHumidity = new(0m, DateTimeOffset.Now);
            CisternFillLevel = new(0m, DateTimeOffset.Now);
        }
    }
}
