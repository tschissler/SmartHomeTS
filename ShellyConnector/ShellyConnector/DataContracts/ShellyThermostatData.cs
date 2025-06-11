using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellyConnector.DataContracts
{
    public record ShellyThermostatData (
        decimal TargetTemperature, 
        decimal CurrentTemperature, 
        int ValvePosition,
        int BatteryLevel, 
        bool IsConnected, 
        DateTimeOffset LastUpdated);
}
