using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChargingController
{
    public record VWData (
        string model,
        int battery,
        DateTimeOffset batteryupdate,
        string chargingstate);
}
