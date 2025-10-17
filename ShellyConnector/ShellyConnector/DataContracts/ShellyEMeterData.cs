using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellyConnector.DataContracts
{
    public class Emeter
    {
        public double Power { get; set; }
        public double Pf { get; set; }
        public double Current { get; set; }
        public double Voltage { get; set; }
        public bool Is_Valid { get; set; }
        public double Total { get; set; }
        public double TotalReturned { get; set; }
    }

    public class ShellyEmeterData
    {
        public List<Emeter> Emeters { get; set; }
    }


    public class ShellyEmeterGen3PowerData
    {
        public int id { get; set; }
        public float a_current { get; set; }
        public float a_voltage { get; set; }
        public float a_act_power { get; set; }
        public float a_aprt_power { get; set; }
        public float a_pf { get; set; }
        public float a_freq { get; set; }
        public float b_current { get; set; }
        public float b_voltage { get; set; }
        public float b_act_power { get; set; }
        public float b_aprt_power { get; set; }
        public float b_pf { get; set; }
        public float b_freq { get; set; }
        public float c_current { get; set; }
        public float c_voltage { get; set; }
        public float c_act_power { get; set; }
        public float c_aprt_power { get; set; }
        public float c_pf { get; set; }
        public float c_freq { get; set; }
        public object n_current { get; set; }
        public float total_current { get; set; }
        public float total_act_power { get; set; }
        public float total_aprt_power { get; set; }
        public object[] user_calibrated_phase { get; set; }
    }


    public class ShellyEmeterGen3EnergyData
    {
        public int id { get; set; }
        public float a_total_act_energy { get; set; }
        public float a_total_act_ret_energy { get; set; }
        public float b_total_act_energy { get; set; }
        public float b_total_act_ret_energy { get; set; }
        public float c_total_act_energy { get; set; }
        public float c_total_act_ret_energy { get; set; }
        public float total_act { get; set; }
        public float total_act_ret { get; set; }
    }

}
