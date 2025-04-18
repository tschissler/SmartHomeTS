using Newtonsoft.Json;

namespace KebaConnector
{

    public class KebaDeviceStatusData
    {
        /// <summary>
        /// Device status
        /// 0 = Startup
        /// 1 = Not ready for charging
        ///     Charging station is not connected to a vehicle, is locked by the authorization function or another mechanism.
        /// 2 = Ready for charging and waiting for reaction from vehicle
        /// 3 = Charging
        /// 4 = Error is present
        /// 5 = Charging process temporarily interrupted because temperature is too high or any other voter denies.
        /// </summary>
        public int State { get; set; }  //

        public int Error1 { get; set; }
        public int Error2 { get; set; }

        /// <summary>
        /// Plug status of the cable
        /// 0 = No cable is plugged.
        /// 1 = Cable is plugged into charging station.
        /// 3 = Cable is plugged into charging station and locked.
        ///     This is the default idle state for all devices with permanently attached cable.
        /// 5 = Cable is plugged into charging station and vehicle but not locked.
        /// 7 = Cable is plugged into charging station and vehicle, furthermore the cable is locked.
        ///     Charging is not possible until plug state “7” is reached.
        /// </summary>
        [JsonProperty("Plug")]
        public int PlugStatus { get; set; }

        /// <summary>
        /// Authorization is activated (e.g. RFID card)
        /// </summary>
        [JsonProperty("AuthON")]
        public int AuthorizationFunctionActivated { get; set; }

        /// <summary>
        /// Defines if authorization is required
        /// </summary>
        [JsonProperty("Authreq")]
        public int AuthorizationRequired { get; set; }

        /// <summary>
        /// Defines if charging can be enabled (1) or not (0)
        /// </summary>
        [JsonProperty("Enable sys")]
        public int ChargingEnabled { get; set; }

        /// <summary>
        /// Defines if the device is enabled (1) or disabled (0) e.g. via ena 0
        /// </summary>
        [JsonProperty("Enable user")]
        public int DeviceEnabled { get; set; }

        /// <summary>
        /// Current value in mA offered to the vehicle via control pilot signalization. (Signal type: PWM)
        /// </summary>
        [JsonProperty("Max curr")]
        public int MaxCurrency { get; set; }

        /// <summary>
        /// Duty cycle of the control pilot signal. The unit displayed is not % but 0.1%, which means that the value “1000” stands for a duty cycle of 100%. 
        /// For more information concerning the control pilot refer to IEC 61851-1.
        /// </summary>
        [JsonProperty("Max curr %")]
        public int MaxcurrPercent { get; set; }

        /// <summary>
        /// Maximum current value in mA that can be supported by the hardware of the device.
        /// This value represents the minimum of the DIP switch settings, cable coding and temperature monitoring function.
        /// </summary>
        [JsonProperty("Curr HW")]
        public int CurrencySupportedByDevice { get; set; }

        /// <summary>
        /// Current setting in mA defined via UDP current commands. (Default: 63000 mA)
        /// </summary>
        [JsonProperty("Curr user")]
        public int TargetCurrency { get; set; }

        /// <summary>
        /// Current setting in mA defined via fail-safe function.
        /// </summary>
        [JsonProperty("Curr FS")]
        public int FailSaveCurrency { get; set; }

        /// <summary>
        /// Communication timeout in seconds before triggering the Failsafe function.
        /// </summary>
        [JsonProperty("Tmo FS")]
        public int FailSaveTimeOut { get; set; }

        /// <summary>
        /// Current value in mA that will replace the setting in the “Curr user” field as soon as “TimerDuration” expires.
        /// </summary>
        [JsonProperty("Curr timer")]
        public int TimerCurrency { get; set; }

        /// <summary>
        /// Timeout in seconds before the current setting defined by the last currtime command will be applied.
        /// </summary>
        [JsonProperty("Tmo CT")]
        public int TimerDuration { get; set; }

        /// <summary>
        /// Energy value in 0.1 Wh defined by the last TargetEnergy command(TargetEnergy = 100000 specifies 10 kWh). Max.value is 99999999.9 Wh
        /// </summary>
        [JsonProperty("Setenergy")]
        public int TargetEnergy { get; set; }

        /// <summary>
        /// State of the output X2 relay
        /// 0 = Closed
        /// 1 = Open
        /// >= 10 = Pulse output with the specified number of pulses(pulses / kWh) and is stored in the EEPROM; reasonably usable up to 150.
        /// </summary>
        public int Output { get; set; }

        /// <summary>
        /// State of the input X1.
        /// </summary>
        public int Input { get; set; }

        /// <summary>
        /// Serial number of the device.
        /// </summary>
        public string Serial { get; set; } = String.Empty;

        /// <summary>
        /// Current state of the system clock in seconds from the last startup of the device.
        /// </summary>
        [JsonProperty("Sec")]
        public int SystemClock { get; set; }

        /// <summary>
        /// Measured voltage value on phase 1 in V
        /// </summary>
        [JsonProperty("U1")]
        public int VoltagePhase1 { get; set; }

        /// <summary>
        /// Measured voltage value on phase 2 in V
        /// </summary>
        [JsonProperty("U2")]
        public int VoltagePhase2 { get; set; }

        /// <summary>
        /// Measured voltage value on phase 3 in V
        /// </summary>
        [JsonProperty("U3")]
        public int VoltagePhase3 { get; set; }

        /// <summary>
        /// Measured current value on phase 1 in mA
        /// </summary>
        [JsonProperty("I1")]
        public int CurrentPhase1 { get; set; }

        /// <summary>
        /// Measured current value on phase 2 in mA
        /// </summary>
        [JsonProperty("I2")]
        public int CurrentPhase2 { get; set; }

        /// <summary>
        /// Measured current value on phase 3 in mA
        /// </summary>
        [JsonProperty("I3")]
        public int CurrentPhase3 { get; set; }

        /// <summary>
        /// Power in mW (effective power).
        /// </summary>
        [JsonProperty("P")]
        public int CurrentChargingPower { get; set; }

        /// <summary>
        /// Current power factor (cosphi). The unit displayed is not % but 0.1%
        /// </summary>
        [JsonProperty("PF")]
        public int PowerFactor { get; set; }

        /// <summary>
        /// Energy transferred in the current charging session in 0.1 Wh.This value is reset at the beginning of a new charging session.
        /// </summary>
        [JsonProperty("E pres")]
        public int EnergyCurrentChargingSession { get; set; }

        /// <summary>
        /// Total energy consumption (persistent, device related) in 0.1 Wh.
        /// </summary>
        [JsonProperty("E total")]
        public int EnergyTotal { get; set; }
    }

}