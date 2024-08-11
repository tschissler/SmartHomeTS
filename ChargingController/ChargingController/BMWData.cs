using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChargingController
{
    public record BMWData (
        string brand,
        string name,
        int battery,
        DateTimeOffset last_update);
}
