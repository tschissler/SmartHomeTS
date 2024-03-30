using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KebaConnector
{
    public record KebaData (
        PlugStatus PlugStatus,
        bool ChargingEnabled,
        bool DeviceEnabled,
        int MaxCurrencyOfferedByChargingStation,
        int MaxCurrencyOfferedByChargingStationPercent,
        int MaxCurrencyPossibleByChargingStation,
        int TargetCurrency,
        int TargetEnergy,
        string SerialNumber,
        int VoltagePhase1,
        int VoltagePhase2,
        int VoltagePhase3,
        int CurrencyPhase1,
        int CurrencyPhase2,
        int CurrencyPhase3,
        int CurrentChargingPower,
        int EnergyCurrentChargingSession,
        int EnergyTotal
        );

    public enum PlugStatus
    {
        CableNotPluggedIn = 0,
        CablePluggedInChargingStation = 1,
        CablePluggedInChargingStationAndLocked = 3,
        CablePluggedInChargingStationAndVehicleButNotLocked = 5,
        CablePluggedInChargingStationAndVehicleAndLocked = 7,
    }
}
