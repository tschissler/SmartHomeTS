using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedContracts
{
    public record ShellyPowerData(
        DateTimeOffset Timestamp,
        decimal Power,
        decimal Voltage,
        decimal TotalPower
    )
    {
        public const MeasurementCategory Category = MeasurementCategory.Electricity;
        public const string SensorType = "Shelly";
        public const MeasurementSubCategory SubCategory = MeasurementSubCategory.Consumption;
    }
}
